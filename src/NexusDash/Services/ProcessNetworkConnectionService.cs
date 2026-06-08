using NexusDash.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NexusDash.Services
{
    public sealed class ProcessNetworkConnectionService
    {
        private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
        private readonly IProcessCommandRunner _processCommandRunner;

        public ProcessNetworkConnectionService(IProcessCommandRunner processCommandRunner)
        {
            _processCommandRunner = processCommandRunner;
        }

        public Task<IReadOnlyList<ProcessNetworkConnection>> GetConnectionsAsync()
        {
            return Task.Run<IReadOnlyList<ProcessNetworkConnection>>(GetConnections);
        }

        private IReadOnlyList<ProcessNetworkConnection> GetConnections()
        {
            if (OperatingSystem.IsWindows())
            {
                return ReadWindowsNetstat();
            }

            if (OperatingSystem.IsLinux())
            {
                return ReadLinuxProcNet();
            }

            if (OperatingSystem.IsMacOS())
            {
                return ReadMacOsLsof();
            }

            return [];
        }

        private IReadOnlyList<ProcessNetworkConnection> ReadWindowsNetstat()
        {
            var result = new List<ProcessNetworkConnection>();
            var output = RunCommand("netstat", "-ano");

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("TCP", StringComparison.OrdinalIgnoreCase) &&
                    !trimmed.StartsWith("UDP", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parts = WhitespaceRegex.Split(trimmed);
                if (parts.Length < 4 ||
                    !int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                {
                    continue;
                }

                var protocol = parts[0].ToUpperInvariant();
                if (!TryParseEndpoint(parts[1], out var localAddress, out var localPort))
                {
                    continue;
                }

                var remoteAddress = "*";
                var remotePort = 0;
                var state = protocol == "TCP" ? "" : "Open";

                if (protocol == "TCP")
                {
                    if (parts.Length < 5)
                    {
                        continue;
                    }

                    TryParseEndpoint(parts[2], out remoteAddress, out remotePort);
                    state = parts[3];
                }
                else if (parts.Length >= 4)
                {
                    TryParseEndpoint(parts[2], out remoteAddress, out remotePort);
                }

                result.Add(new ProcessNetworkConnection
                {
                    Protocol = protocol,
                    Pid = pid,
                    LocalAddress = localAddress,
                    LocalPort = localPort,
                    RemoteAddress = remoteAddress,
                    RemotePort = remotePort,
                    State = state
                });
            }

            return SortConnections(result);
        }

        private static IReadOnlyList<ProcessNetworkConnection> ReadLinuxProcNet()
        {
            var socketOwners = BuildLinuxSocketOwnerMap();
            var result = new List<ProcessNetworkConnection>();

            ReadLinuxProcNetFile("/proc/net/tcp", "TCP", isIpv6: false, socketOwners, result);
            ReadLinuxProcNetFile("/proc/net/tcp6", "TCP", isIpv6: true, socketOwners, result);
            ReadLinuxProcNetFile("/proc/net/udp", "UDP", isIpv6: false, socketOwners, result);
            ReadLinuxProcNetFile("/proc/net/udp6", "UDP", isIpv6: true, socketOwners, result);

            return SortConnections(result);
        }

        private static void ReadLinuxProcNetFile(
            string path,
            string protocol,
            bool isIpv6,
            IReadOnlyDictionary<ulong, ProcessIdentity> socketOwners,
            IList<ProcessNetworkConnection> result)
        {
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                foreach (var line in File.ReadLines(path).Skip(1))
                {
                    var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 10 ||
                        !ulong.TryParse(parts[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out var inode))
                    {
                        continue;
                    }

                    if (!TryParseLinuxEndpoint(parts[1], isIpv6, out var localAddress, out var localPort) ||
                        !TryParseLinuxEndpoint(parts[2], isIpv6, out var remoteAddress, out var remotePort))
                    {
                        continue;
                    }

                    socketOwners.TryGetValue(inode, out var owner);
                    result.Add(new ProcessNetworkConnection
                    {
                        Protocol = protocol,
                        Pid = owner.Pid == 0 ? null : owner.Pid,
                        ProcessName = owner.Name,
                        LocalAddress = localAddress,
                        LocalPort = localPort,
                        RemoteAddress = remoteAddress,
                        RemotePort = remotePort,
                        State = protocol == "TCP" ? MapLinuxTcpState(parts[3]) : "Open"
                    });
                }
            }
            catch
            {
                // Hardened systems may restrict /proc/net reads.
            }
        }

        private static IReadOnlyDictionary<ulong, ProcessIdentity> BuildLinuxSocketOwnerMap()
        {
            var result = new Dictionary<ulong, ProcessIdentity>();

            try
            {
                foreach (var processDirectory in Directory.EnumerateDirectories("/proc"))
                {
                    var name = Path.GetFileName(processDirectory);
                    if (!int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
                    {
                        continue;
                    }

                    var processName = ReadLinuxProcessName(processDirectory);
                    var fdDirectory = Path.Combine(processDirectory, "fd");
                    if (!Directory.Exists(fdDirectory))
                    {
                        continue;
                    }

                    string[] fdPaths;
                    try
                    {
                        fdPaths = Directory.EnumerateFiles(fdDirectory).ToArray();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var fdPath in fdPaths)
                    {
                        string? linkTarget;
                        try
                        {
                            linkTarget = new FileInfo(fdPath).LinkTarget;
                        }
                        catch
                        {
                            continue;
                        }

                        if (TryParseLinuxSocketInode(linkTarget, out var inode))
                        {
                            result[inode] = new ProcessIdentity(pid, processName);
                        }
                    }
                }
            }
            catch
            {
                // /proc/<pid>/fd can be restricted for other users.
            }

            return result;
        }

        private static bool TryParseLinuxSocketInode(string? linkTarget, out ulong inode)
        {
            inode = 0;
            const string prefix = "socket:[";
            if (string.IsNullOrWhiteSpace(linkTarget) ||
                !linkTarget.StartsWith(prefix, StringComparison.Ordinal) ||
                !linkTarget.EndsWith(']'))
            {
                return false;
            }

            var value = linkTarget[prefix.Length..^1];
            return ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out inode);
        }

        private static string? ReadLinuxProcessName(string processDirectory)
        {
            try
            {
                var value = File.ReadAllText(Path.Combine(processDirectory, "comm")).Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
            catch
            {
                return null;
            }
        }

        private IReadOnlyList<ProcessNetworkConnection> ReadMacOsLsof()
        {
            var result = new List<ProcessNetworkConnection>();
            var output = RunCommand("lsof", "-nP -iTCP -iUDP");

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                var parts = WhitespaceRegex.Split(trimmed, 9);
                if (parts.Length < 9 ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                {
                    continue;
                }

                var protocol = parts[7].ToUpperInvariant();
                if (protocol is not ("TCP" or "UDP"))
                {
                    continue;
                }

                var name = parts[8];
                var state = ExtractTrailingState(name);
                if (state.Length > 0)
                {
                    name = name[..name.LastIndexOf(" (", StringComparison.Ordinal)].Trim();
                }

                var endpointParts = name.Split("->", 2, StringSplitOptions.TrimEntries);
                if (!TryParseEndpoint(endpointParts[0], out var localAddress, out var localPort))
                {
                    continue;
                }

                var remoteAddress = "*";
                var remotePort = 0;
                if (endpointParts.Length == 2)
                {
                    TryParseEndpoint(endpointParts[1], out remoteAddress, out remotePort);
                }

                result.Add(new ProcessNetworkConnection
                {
                    Protocol = protocol,
                    Pid = pid,
                    ProcessName = parts[0],
                    LocalAddress = localAddress,
                    LocalPort = localPort,
                    RemoteAddress = remoteAddress,
                    RemotePort = remotePort,
                    State = state.Length > 0 ? state : protocol == "TCP" ? "LISTEN" : "Open"
                });
            }

            return SortConnections(result);
        }

        private static bool TryParseEndpoint(string value, out string address, out int port)
        {
            address = "*";
            port = 0;

            if (string.IsNullOrWhiteSpace(value) || value.Equals("*:*", StringComparison.Ordinal))
            {
                return true;
            }

            value = value.Trim();
            string portText;
            if (value.StartsWith("[", StringComparison.Ordinal))
            {
                var endBracket = value.LastIndexOf(']');
                if (endBracket < 0)
                {
                    return false;
                }

                address = value[1..endBracket];
                portText = endBracket + 2 <= value.Length ? value[(endBracket + 2)..] : "";
            }
            else
            {
                var separator = value.LastIndexOf(':');
                if (separator < 0)
                {
                    address = value;
                    return true;
                }

                address = value[..separator];
                portText = value[(separator + 1)..];
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                address = "*";
            }

            if (portText.Equals("*", StringComparison.Ordinal))
            {
                port = 0;
                return true;
            }

            return int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out port);
        }

        private static bool TryParseLinuxEndpoint(
            string value,
            bool isIpv6,
            out string address,
            out int port)
        {
            address = "*";
            port = 0;

            var parts = value.Split(':', 2);
            if (parts.Length != 2 ||
                !int.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out port))
            {
                return false;
            }

            address = isIpv6
                ? DecodeLinuxIpv6Address(parts[0])
                : DecodeLinuxIpv4Address(parts[0]);
            return true;
        }

        private static string DecodeLinuxIpv4Address(string value)
        {
            if (value.Length != 8)
            {
                return value;
            }

            var bytes = Enumerable.Range(0, 4)
                .Select(index => byte.Parse(value.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                .Reverse()
                .ToArray();
            return new IPAddress(bytes).ToString();
        }

        private static string DecodeLinuxIpv6Address(string value)
        {
            if (value.Length != 32)
            {
                return value;
            }

            var bytes = Enumerable.Range(0, 16)
                .Select(index => byte.Parse(value.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                .ToArray();

            for (var index = 0; index < bytes.Length; index += 4)
            {
                Array.Reverse(bytes, index, 4);
            }

            return new IPAddress(bytes).ToString();
        }

        private static string MapLinuxTcpState(string value)
        {
            return value.ToUpperInvariant() switch
            {
                "01" => "ESTABLISHED",
                "02" => "SYN_SENT",
                "03" => "SYN_RECV",
                "04" => "FIN_WAIT1",
                "05" => "FIN_WAIT2",
                "06" => "TIME_WAIT",
                "07" => "CLOSE",
                "08" => "CLOSE_WAIT",
                "09" => "LAST_ACK",
                "0A" => "LISTEN",
                "0B" => "CLOSING",
                "0C" => "NEW_SYN_RECV",
                _ => value
            };
        }

        private static string ExtractTrailingState(string value)
        {
            var start = value.LastIndexOf(" (", StringComparison.Ordinal);
            if (start < 0 || !value.EndsWith(')'))
            {
                return "";
            }

            return value[(start + 2)..^1];
        }

        private string RunCommand(string fileName, string arguments)
        {
            try
            {
                return _processCommandRunner.ReadOutput(fileName, arguments);
            }
            catch
            {
                return "";
            }
        }

        private static IReadOnlyList<ProcessNetworkConnection> SortConnections(
            IEnumerable<ProcessNetworkConnection> connections)
        {
            return connections
                .OrderBy(static connection => connection.Protocol, StringComparer.Ordinal)
                .ThenBy(static connection => connection.LocalPort)
                .ThenBy(static connection => connection.LocalAddress, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static connection => connection.RemoteAddress, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static connection => connection.RemotePort)
                .ThenBy(static connection => connection.Pid ?? int.MaxValue)
                .ToArray();
        }

        private readonly record struct ProcessIdentity(int Pid, string? Name);
    }
}
