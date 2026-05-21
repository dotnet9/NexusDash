using NexusDash.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

#if WINDOWS
using System.Drawing;
using System.Drawing.Imaging;
using System.Management;
#endif

namespace NexusDash.Services
{
    public sealed class ProcessTelemetryService
    {
        private readonly Dictionary<int, ProcessSample> _previousSamples = new();
        private readonly Dictionary<string, string?> _publisherCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string?> _descriptionCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, byte[]?> _iconCache = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<ProcessMetrics>> GetProcessesAsync()
        {
            return Task.Run<IReadOnlyList<ProcessMetrics>>(GetProcesses);
        }

        private IReadOnlyList<ProcessMetrics> GetProcesses()
        {
            var now = DateTime.UtcNow;
            var metadata = PlatformProcessMetadataReader.ReadAll();
            var currentPids = new HashSet<int>();
            var result = new List<ProcessMetrics>();

            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    ProcessMetrics? metrics = null;
                    try
                    {
                        currentPids.Add(process.Id);
                        metadata.TryGetValue(process.Id, out var platform);

                        var totalProcessorTime = TryGetTotalProcessorTime(process);
                        var cpuPercent = CalculateCpuPercent(process.Id, totalProcessorTime, now);
                        var readBytes = platform?.ReadTransferBytes;
                        var writeBytes = platform?.WriteTransferBytes;
                        var (readRate, writeRate) = CalculateDiskRates(process.Id, readBytes, writeBytes, now);

                        var path = FirstNonEmpty(platform?.ExecutablePath, TryGetExecutablePath(process));
                        var commandLine = FirstNonEmpty(platform?.CommandLine, path, process.ProcessName);
                        var publisher = TryGetPublisher(path);
                        var rawName = FirstNonEmpty(process.ProcessName, platform?.Name, "Unknown")!;
                        var displayName = FirstNonEmpty(platform?.ServiceDisplayName, TryGetFileDescription(path), rawName)!;
                        var hasMainWindow = TryHasMainWindow(process);
                        var category = ClassifyProcess(platform, path, rawName, hasMainWindow);

                        metrics = new ProcessMetrics
                        {
                            Pid = process.Id,
                            ParentPid = platform?.ParentPid,
                            Name = displayName,
                            RawName = rawName,
                            Publisher = publisher,
                            Category = category,
                            IconBytes = TryGetProcessIconBytes(path),
                            CpuPercent = cpuPercent,
                            WorkingSetBytes = TryGetWorkingSet(process),
                            DiskReadBytesPerSecond = readRate,
                            DiskWriteBytesPerSecond = writeRate,
                            CommandLine = commandLine,
                            ExecutablePath = path,
                            StartTime = TryGetStartTime(process),
                            IsAccessDenied = false
                        };
                    }
                    catch
                    {
                        try
                        {
                            metrics = new ProcessMetrics
                            {
                                Pid = process.Id,
                                Name = process.ProcessName,
                                RawName = process.ProcessName,
                                Category = ClassifyProcess(null, null, process.ProcessName, hasMainWindow: false),
                                IsAccessDenied = true
                            };
                        }
                        catch
                        {
                            // The process exited while being sampled.
                        }
                    }

                    if (metrics is not null)
                    {
                        result.Add(metrics);
                    }
                }
            }

            foreach (var pid in _previousSamples.Keys.Where(pid => !currentPids.Contains(pid)).ToArray())
            {
                _previousSamples.Remove(pid);
            }

            return result
                .OrderBy(static p => p.ParentPid ?? int.MaxValue)
                .ThenBy(static p => p.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(static p => p.Pid)
                .ToList();
        }

        public static int EndProcess(int pid, bool entireProcessTree)
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree);
            return pid;
        }

        private double CalculateCpuPercent(int pid, TimeSpan? totalProcessorTime, DateTime now)
        {
            if (totalProcessorTime is null)
            {
                return 0;
            }

            if (!_previousSamples.TryGetValue(pid, out var previous) || previous.TotalProcessorTime is null)
            {
                _previousSamples[pid] = previous with
                {
                    CpuTimestamp = now,
                    TotalProcessorTime = totalProcessorTime
                };
                return 0;
            }

            var elapsedMs = Math.Max((now - previous.CpuTimestamp).TotalMilliseconds, 1);
            var cpuDeltaMs = Math.Max((totalProcessorTime.Value - previous.TotalProcessorTime.Value).TotalMilliseconds, 0);
            var cpuPercent = cpuDeltaMs / elapsedMs / Math.Max(Environment.ProcessorCount, 1) * 100;

            _previousSamples[pid] = previous with
            {
                CpuTimestamp = now,
                TotalProcessorTime = totalProcessorTime
            };

            return Math.Clamp(cpuPercent, 0, 100);
        }

        private (double readRate, double writeRate) CalculateDiskRates(
            int pid,
            ulong? readBytes,
            ulong? writeBytes,
            DateTime now)
        {
            if (readBytes is null && writeBytes is null)
            {
                return (0, 0);
            }

            _previousSamples.TryGetValue(pid, out var previous);
            var elapsedSeconds = previous.IoTimestamp == default
                ? 0.001
                : Math.Max((now - previous.IoTimestamp).TotalSeconds, 0.001);
            var readRate = 0d;
            var writeRate = 0d;

            if (readBytes is not null && previous.ReadTransferBytes is not null && readBytes >= previous.ReadTransferBytes)
            {
                readRate = (readBytes.Value - previous.ReadTransferBytes.Value) / elapsedSeconds;
            }

            if (writeBytes is not null && previous.WriteTransferBytes is not null && writeBytes >= previous.WriteTransferBytes)
            {
                writeRate = (writeBytes.Value - previous.WriteTransferBytes.Value) / elapsedSeconds;
            }

            _previousSamples[pid] = previous with
            {
                IoTimestamp = now,
                ReadTransferBytes = readBytes ?? previous.ReadTransferBytes,
                WriteTransferBytes = writeBytes ?? previous.WriteTransferBytes
            };

            return (readRate, writeRate);
        }

        private static TimeSpan? TryGetTotalProcessorTime(Process process)
        {
            try
            {
                return process.TotalProcessorTime;
            }
            catch
            {
                return null;
            }
        }

        private static ulong TryGetWorkingSet(Process process)
        {
            try
            {
                return (ulong)Math.Max(process.WorkingSet64, 0);
            }
            catch
            {
                return 0;
            }
        }

        private static DateTime? TryGetStartTime(Process process)
        {
            try
            {
                return process.StartTime;
            }
            catch
            {
                return null;
            }
        }

        private static string? TryGetExecutablePath(Process process)
        {
            try
            {
                return process.MainModule?.FileName;
            }
            catch
            {
                return null;
            }
        }

        private string? TryGetPublisher(string? executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return null;
            }

            if (_publisherCache.TryGetValue(executablePath, out var cachedPublisher))
            {
                return cachedPublisher;
            }

            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
                var publisher = FirstNonEmpty(versionInfo.CompanyName, versionInfo.LegalCopyright);
                _publisherCache[executablePath] = publisher;
                return publisher;
            }
            catch
            {
                _publisherCache[executablePath] = null;
                return null;
            }
        }

        private string? TryGetFileDescription(string? executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return null;
            }

            if (_descriptionCache.TryGetValue(executablePath, out var cachedDescription))
            {
                return cachedDescription;
            }

            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
                var fileName = Path.GetFileNameWithoutExtension(executablePath);
                var description = FirstNonEmpty(versionInfo.FileDescription, versionInfo.ProductName);
                if (string.Equals(description, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    description = null;
                }

                _descriptionCache[executablePath] = description;
                return description;
            }
            catch
            {
                _descriptionCache[executablePath] = null;
                return null;
            }
        }

        private byte[]? TryGetProcessIconBytes(string? executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return null;
            }

            if (_iconCache.TryGetValue(executablePath, out var cachedIcon))
            {
                return cachedIcon;
            }

#if WINDOWS
            try
            {
                if (!File.Exists(executablePath))
                {
                    _iconCache[executablePath] = null;
                    return null;
                }

                using var icon = Icon.ExtractAssociatedIcon(executablePath);
                if (icon is null)
                {
                    _iconCache[executablePath] = null;
                    return null;
                }

                using var bitmap = icon.ToBitmap();
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png);
                var bytes = stream.ToArray();
                _iconCache[executablePath] = bytes;
                return bytes;
            }
            catch
            {
                _iconCache[executablePath] = null;
                return null;
            }
#else
            _iconCache[executablePath] = null;
            return null;
#endif
        }

        private static bool TryHasMainWindow(Process process)
        {
            try
            {
                return process.MainWindowHandle != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        private static ProcessCategory ClassifyProcess(
            PlatformProcessMetadata? metadata,
            string? executablePath,
            string rawName,
            bool hasMainWindow)
        {
            if (hasMainWindow)
            {
                return ProcessCategory.Application;
            }

            if (IsWindowsProcess(metadata, executablePath, rawName))
            {
                return ProcessCategory.WindowsProcess;
            }

            return ProcessCategory.BackgroundProcess;
        }

        private static bool IsWindowsProcess(
            PlatformProcessMetadata? metadata,
            string? executablePath,
            string rawName)
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            if (IsKnownWindowsProcessName(rawName))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(metadata?.ServiceDisplayName))
            {
                return true;
            }

            return IsWindowsSystemPath(executablePath) || IsMicrosoftWindowsAppPath(executablePath);
        }

        private static bool IsKnownWindowsProcessName(string rawName)
        {
            var normalized = rawName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(rawName)
                : rawName;

            return normalized.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Idle", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("Registry", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("smss", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("csrss", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("wininit", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("winlogon", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("services", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("lsass", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWindowsSystemPath(string? executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return false;
            }

            var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            return !string.IsNullOrWhiteSpace(windowsDirectory) &&
                   executablePath.StartsWith(windowsDirectory, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMicrosoftWindowsAppPath(string? executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return false;
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (string.IsNullOrWhiteSpace(programFiles))
            {
                return false;
            }

            var microsoftWindowsApps = Path.Combine(programFiles, "WindowsApps", "Microsoft.");
            return executablePath.StartsWith(microsoftWindowsApps, StringComparison.OrdinalIgnoreCase);
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        private readonly record struct ProcessSample(
            DateTime CpuTimestamp,
            DateTime IoTimestamp,
            TimeSpan? TotalProcessorTime,
            ulong? ReadTransferBytes,
            ulong? WriteTransferBytes);

        private sealed class PlatformProcessMetadata
        {
            public int? ParentPid { get; init; }
            public string? Name { get; init; }
            public string? ServiceDisplayName { get; init; }
            public string? ServiceGroupName { get; init; }
            public string? CommandLine { get; init; }
            public string? ExecutablePath { get; init; }
            public ulong? ReadTransferBytes { get; init; }
            public ulong? WriteTransferBytes { get; init; }
        }

        private static class PlatformProcessMetadataReader
        {
            public static IReadOnlyDictionary<int, PlatformProcessMetadata> ReadAll()
            {
                if (OperatingSystem.IsWindows())
                {
                    return ReadWindows();
                }

                if (OperatingSystem.IsLinux())
                {
                    return ReadLinux();
                }

                if (OperatingSystem.IsMacOS())
                {
                    return ReadMacOs();
                }

                return new Dictionary<int, PlatformProcessMetadata>();
            }

            private static IReadOnlyDictionary<int, PlatformProcessMetadata> ReadWindows()
            {
#if WINDOWS
                var result = new Dictionary<int, PlatformProcessMetadata>();

                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT ProcessId, ParentProcessId, Name, CommandLine, ExecutablePath, ReadTransferCount, WriteTransferCount FROM Win32_Process");

                    foreach (ManagementObject item in searcher.Get())
                    {
                        using (item)
                        {
                            var pid = Convert.ToInt32(item["ProcessId"], CultureInfo.InvariantCulture);
                            var commandLine = item["CommandLine"] as string;
                            result[pid] = new PlatformProcessMetadata
                            {
                                ParentPid = TryConvertInt32(item["ParentProcessId"]),
                                Name = item["Name"] as string,
                                ServiceGroupName = ExtractWindowsServiceGroupName(commandLine),
                                CommandLine = commandLine,
                                ExecutablePath = item["ExecutablePath"] as string,
                                ReadTransferBytes = TryConvertUInt64(item["ReadTransferCount"]),
                                WriteTransferBytes = TryConvertUInt64(item["WriteTransferCount"])
                            };
                        }
                    }
                }
                catch
                {
                    // WMI can be disabled or unavailable.
                }

                foreach (var serviceGroup in ReadWindowsServiceDisplayNames())
                {
                    if (!result.TryGetValue(serviceGroup.Key, out var processMetadata))
                    {
                        continue;
                    }

                    result[serviceGroup.Key] = new PlatformProcessMetadata
                    {
                        ParentPid = processMetadata.ParentPid,
                        Name = processMetadata.Name,
                        ServiceDisplayName = FormatWindowsServiceDisplayName(processMetadata.ServiceGroupName, serviceGroup.Value),
                        ServiceGroupName = processMetadata.ServiceGroupName,
                        CommandLine = processMetadata.CommandLine,
                        ExecutablePath = processMetadata.ExecutablePath,
                        ReadTransferBytes = processMetadata.ReadTransferBytes,
                        WriteTransferBytes = processMetadata.WriteTransferBytes
                    };
                }

                return result;
#else
                return new Dictionary<int, PlatformProcessMetadata>();
#endif
            }

            private static IReadOnlyDictionary<int, PlatformProcessMetadata> ReadLinux()
            {
                var result = new Dictionary<int, PlatformProcessMetadata>();

                try
                {
                    foreach (var directory in Directory.EnumerateDirectories("/proc"))
                    {
                        var name = Path.GetFileName(directory);
                        if (!int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
                        {
                            continue;
                        }

                        var metadata = ReadLinuxProcess(pid, directory);
                        if (metadata is not null)
                        {
                            result[pid] = metadata;
                        }
                    }
                }
                catch
                {
                    // /proc can be restricted in containers or hardened systems.
                }

                return result;
            }

            private static PlatformProcessMetadata? ReadLinuxProcess(int pid, string processDirectory)
            {
                try
                {
                    var statPath = Path.Combine(processDirectory, "stat");
                    var stat = File.ReadAllText(statPath);
                    var closingParen = stat.LastIndexOf(')');
                    int? parentPid = null;
                    string? name = null;

                    if (closingParen > 0)
                    {
                        var openingParen = stat.IndexOf('(');
                        if (openingParen >= 0 && closingParen > openingParen)
                        {
                            name = stat.Substring(openingParen + 1, closingParen - openingParen - 1);
                        }

                        var afterName = stat[(closingParen + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (afterName.Length >= 2 &&
                            int.TryParse(afterName[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedParent))
                        {
                            parentPid = parsedParent;
                        }
                    }

                    return new PlatformProcessMetadata
                    {
                        ParentPid = parentPid,
                        Name = name,
                        CommandLine = ReadLinuxCommandLine(pid),
                        ExecutablePath = ReadLinuxExecutablePath(pid),
                        ReadTransferBytes = ReadLinuxIoBytes(pid, "read_bytes:"),
                        WriteTransferBytes = ReadLinuxIoBytes(pid, "write_bytes:")
                    };
                }
                catch
                {
                    return null;
                }
            }

            private static string? ReadLinuxCommandLine(int pid)
            {
                try
                {
                    var bytes = File.ReadAllBytes($"/proc/{pid}/cmdline");
                    var text = System.Text.Encoding.UTF8.GetString(bytes)
                        .Replace('\0', ' ')
                        .Trim();
                    return string.IsNullOrWhiteSpace(text) ? null : text;
                }
                catch
                {
                    return null;
                }
            }

            private static string? ReadLinuxExecutablePath(int pid)
            {
                try
                {
                    return File.ResolveLinkTarget($"/proc/{pid}/exe", true)?.FullName;
                }
                catch
                {
                    return null;
                }
            }

            private static ulong? ReadLinuxIoBytes(int pid, string key)
            {
                try
                {
                    foreach (var line in File.ReadLines($"/proc/{pid}/io"))
                    {
                        if (!line.StartsWith(key, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        var valueText = line[key.Length..].Trim();
                        return ulong.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                            ? value
                            : null;
                    }
                }
                catch
                {
                }

                return null;
            }

            private static IReadOnlyDictionary<int, PlatformProcessMetadata> ReadMacOs()
            {
                var result = new Dictionary<int, PlatformProcessMetadata>();

                try
                {
                    var output = SystemMonitorService.RunProcessAndReadOutput("ps", "-axo pid=,ppid=,command=");
                    foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var trimmed = line.Trim();
                        var parts = trimmed.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2 ||
                            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) ||
                            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parentPid))
                        {
                            continue;
                        }

                        var command = parts.Length == 3 ? parts[2] : null;
                        result[pid] = new PlatformProcessMetadata
                        {
                            ParentPid = parentPid,
                            CommandLine = command,
                            ExecutablePath = command?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                        };
                    }
                }
                catch
                {
                }

                return result;
            }

#if WINDOWS
            private static IReadOnlyDictionary<int, IReadOnlyList<string>> ReadWindowsServiceDisplayNames()
            {
                var result = new Dictionary<int, List<string>>();

                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT ProcessId, Name, DisplayName FROM Win32_Service WHERE ProcessId <> 0");

                    foreach (ManagementObject item in searcher.Get())
                    {
                        using (item)
                        {
                            var pid = TryConvertInt32(item["ProcessId"]);
                            if (pid is null)
                            {
                                continue;
                            }

                            var displayName = FirstNonEmpty(item["DisplayName"] as string, item["Name"] as string);
                            if (string.IsNullOrWhiteSpace(displayName))
                            {
                                continue;
                            }

                            if (!result.TryGetValue(pid.Value, out var services))
                            {
                                services = new List<string>();
                                result[pid.Value] = services;
                            }

                            services.Add(displayName);
                        }
                    }
                }
                catch
                {
                    // Service Control Manager / WMI access can be restricted.
                }

                return result.ToDictionary(
                    static pair => pair.Key,
                    static pair => (IReadOnlyList<string>)pair.Value
                        .Distinct(StringComparer.CurrentCultureIgnoreCase)
                        .OrderBy(static name => name, StringComparer.CurrentCultureIgnoreCase)
                        .ToArray());
            }

            private static string? FormatWindowsServiceDisplayName(string? serviceGroupName, IReadOnlyList<string> serviceNames)
            {
                if (serviceNames.Count == 0)
                {
                    return string.IsNullOrWhiteSpace(serviceGroupName) ? null : serviceGroupName;
                }

                if (serviceNames.Count == 1)
                {
                    return serviceNames[0];
                }

                var groupName = string.IsNullOrWhiteSpace(serviceGroupName) ? serviceNames[0] : serviceGroupName;
                return string.Format(CultureInfo.CurrentCulture, "{0} ({1})", groupName, serviceNames.Count);
            }

            private static string? ExtractWindowsServiceGroupName(string? commandLine)
            {
                if (string.IsNullOrWhiteSpace(commandLine))
                {
                    return null;
                }

                var parts = commandLine.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                for (var index = 0; index < parts.Length - 1; index++)
                {
                    if (!parts[index].Equals("-k", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var groupName = parts[index + 1].Trim('"');
                    return string.IsNullOrWhiteSpace(groupName) ? null : groupName;
                }

                return null;
            }

            private static int? TryConvertInt32(object? value)
            {
                try
                {
                    return value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return null;
                }
            }

            private static ulong? TryConvertUInt64(object? value)
            {
                try
                {
                    return value is null ? null : Convert.ToUInt64(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return null;
                }
            }
#endif
        }
    }
}
