using NexusDash.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NexusDash.Services
{
    public sealed class SystemMonitorService : IDisposable
    {
        private readonly Dictionary<string, NetworkSample> _networkSamples = new(StringComparer.Ordinal);

        public Task<SystemMetrics> GetMetricsAsync()
        {
            return Task.Run(GetMetrics);
        }

        private SystemMetrics GetMetrics()
        {
            var metrics = new SystemMetrics
            {
                Timestamp = DateTime.Now
            };

            metrics.Cpu.CoreCount = Environment.ProcessorCount;
            PopulateMemory(metrics);
            PopulateDisks(metrics);
            PopulateNetwork(metrics);

            return metrics;
        }

        private static void PopulateMemory(SystemMetrics metrics)
        {
            if (OperatingSystem.IsWindows() && TryGetWindowsMemory(out var total, out var available))
            {
                metrics.Memory.TotalBytes = total;
                metrics.Memory.AvailableBytes = available;
                metrics.Memory.UsedBytes = total > available ? total - available : 0;
                return;
            }

            if (OperatingSystem.IsLinux() && TryGetLinuxMemory(out total, out available))
            {
                metrics.Memory.TotalBytes = total;
                metrics.Memory.AvailableBytes = available;
                metrics.Memory.UsedBytes = total > available ? total - available : 0;
                return;
            }

            if (OperatingSystem.IsMacOS() && TryGetMacMemory(out total, out available))
            {
                metrics.Memory.TotalBytes = total;
                metrics.Memory.AvailableBytes = available;
                metrics.Memory.UsedBytes = total > available ? total - available : 0;
                return;
            }

            var gcInfo = GC.GetGCMemoryInfo();
            var fallbackTotal = gcInfo.TotalAvailableMemoryBytes > 0
                ? (ulong)gcInfo.TotalAvailableMemoryBytes
                : (ulong)Math.Max(Environment.WorkingSet, 0);
            var fallbackUsed = (ulong)Math.Max(Environment.WorkingSet, 0);
            metrics.Memory.TotalBytes = fallbackTotal;
            metrics.Memory.UsedBytes = Math.Min(fallbackUsed, fallbackTotal);
            metrics.Memory.AvailableBytes = fallbackTotal - metrics.Memory.UsedBytes;
        }

        private static void PopulateDisks(SystemMetrics metrics)
        {
            try
            {
                foreach (var drive in DriveInfo.GetDrives()
                             .Where(d => d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable))
                {
                    metrics.Disks.Add(new DiskMetrics
                    {
                        Name = drive.Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        DriveLetter = drive.Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        TotalBytes = (ulong)Math.Max(drive.TotalSize, 0),
                        FreeBytes = (ulong)Math.Max(drive.AvailableFreeSpace, 0),
                        UsedBytes = (ulong)Math.Max(drive.TotalSize - drive.AvailableFreeSpace, 0)
                    });
                }
            }
            catch
            {
                // Disk enumeration can fail for protected or transient volumes.
            }
        }

        private void PopulateNetwork(SystemMetrics metrics)
        {
            var now = DateTime.UtcNow;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            ulong totalSent = 0;
            ulong totalReceived = 0;
            double upload = 0;
            double download = 0;

            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                try
                {
                    var stats = adapter.GetIPStatistics();
                    var sent = (ulong)Math.Max(stats.BytesSent, 0);
                    var received = (ulong)Math.Max(stats.BytesReceived, 0);
                    totalSent += sent;
                    totalReceived += received;
                    seen.Add(adapter.Id);

                    if (_networkSamples.TryGetValue(adapter.Id, out var previous))
                    {
                        var seconds = Math.Max((now - previous.Timestamp).TotalSeconds, 0.001);
                        if (sent >= previous.BytesSent)
                        {
                            upload += (sent - previous.BytesSent) / seconds;
                        }

                        if (received >= previous.BytesReceived)
                        {
                            download += (received - previous.BytesReceived) / seconds;
                        }
                    }

                    _networkSamples[adapter.Id] = new NetworkSample(now, sent, received);
                }
                catch
                {
                    // Ignore adapters that disappear while being sampled.
                }
            }

            foreach (var adapterId in _networkSamples.Keys.Where(id => !seen.Contains(id)).ToArray())
            {
                _networkSamples.Remove(adapterId);
            }

            metrics.Network.UploadSpeed = upload;
            metrics.Network.DownloadSpeed = download;
            metrics.Network.TotalBytesUploaded = totalSent;
            metrics.Network.TotalBytesDownloaded = totalReceived;

            try
            {
                metrics.Network.ConnectionCount = IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveTcpConnections()
                    .Length;
            }
            catch
            {
                metrics.Network.ConnectionCount = 0;
            }
        }

        private static bool TryGetWindowsMemory(out ulong totalBytes, out ulong availableBytes)
        {
            totalBytes = 0;
            availableBytes = 0;

            var status = new MemoryStatusEx();
            if (!GlobalMemoryStatusEx(status))
            {
                return false;
            }

            totalBytes = status.TotalPhys;
            availableBytes = status.AvailPhys;
            return totalBytes > 0;
        }

        private static bool TryGetLinuxMemory(out ulong totalBytes, out ulong availableBytes)
        {
            totalBytes = 0;
            availableBytes = 0;

            try
            {
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                    {
                        totalBytes = ParseLinuxMemInfoBytes(line);
                    }
                    else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                    {
                        availableBytes = ParseLinuxMemInfoBytes(line);
                    }
                }
            }
            catch
            {
                return false;
            }

            return totalBytes > 0;
        }

        private static ulong ParseLinuxMemInfoBytes(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 && ulong.TryParse(parts[1], out var kb) ? kb * 1024 : 0;
        }

        private static bool TryGetMacMemory(out ulong totalBytes, out ulong availableBytes)
        {
            totalBytes = 0;
            availableBytes = 0;

            try
            {
                var totalText = RunProcessAndReadOutput("sysctl", "-n hw.memsize").Trim();
                if (!ulong.TryParse(totalText, out totalBytes) || totalBytes == 0)
                {
                    return false;
                }

                var vmStat = RunProcessAndReadOutput("vm_stat", "");
                ulong pageSize = 4096;
                ulong freePages = 0;

                foreach (var line in vmStat.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.Contains("page size of", StringComparison.OrdinalIgnoreCase))
                    {
                        var digits = new string(line.Where(char.IsDigit).ToArray());
                        if (ulong.TryParse(digits, out var parsedPageSize) && parsedPageSize > 0)
                        {
                            pageSize = parsedPageSize;
                        }
                    }
                    else if (line.StartsWith("Pages free:", StringComparison.Ordinal) ||
                             line.StartsWith("Pages inactive:", StringComparison.Ordinal) ||
                             line.StartsWith("Pages speculative:", StringComparison.Ordinal))
                    {
                        var valueText = new string(line.Where(c => char.IsDigit(c)).ToArray());
                        if (ulong.TryParse(valueText, out var pages))
                        {
                            freePages += pages;
                        }
                    }
                }

                availableBytes = Math.Min(totalBytes, freePages * pageSize);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static string RunProcessAndReadOutput(string fileName, string arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(1500);
            return output;
        }

        public void Dispose()
        {
            _networkSamples.Clear();
        }

        private readonly record struct NetworkSample(DateTime Timestamp, ulong BytesSent, ulong BytesReceived);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private sealed class MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;

            public MemoryStatusEx()
            {
                Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
            }
        }
    }
}
