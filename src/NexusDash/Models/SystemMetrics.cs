using System;
using System.Collections.Generic;

namespace NexusDash.Models
{
    public class SystemMetrics
    {
        public CpuMetrics Cpu { get; set; } = new();
        public MemoryMetrics Memory { get; set; } = new();
        public List<DiskMetrics> Disks { get; set; } = new();
        public NetworkMetrics Network { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class CpuMetrics
    {
        public double TotalUsage { get; set; }
        public List<double> CoreUsages { get; set; } = new();
        public double Temperature { get; set; }
        public int CoreCount { get; set; }
    }

    public class MemoryMetrics
    {
        public ulong TotalBytes { get; set; }
        public ulong UsedBytes { get; set; }
        public ulong AvailableBytes { get; set; }
        public double UsagePercentage => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;
        public ulong SwapTotalBytes { get; set; }
        public ulong SwapUsedBytes { get; set; }
    }

    public class DiskMetrics
    {
        public string Name { get; set; } = "";
        public string DriveLetter { get; set; } = "";
        public ulong TotalBytes { get; set; }
        public ulong UsedBytes { get; set; }
        public ulong FreeBytes { get; set; }
        public double UsagePercentage => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;
        public double ReadSpeed { get; set; }
        public double WriteSpeed { get; set; }
    }

    public class NetworkMetrics
    {
        public double UploadSpeed { get; set; }
        public double DownloadSpeed { get; set; }
        public ulong TotalBytesUploaded { get; set; }
        public ulong TotalBytesDownloaded { get; set; }
        public int ConnectionCount { get; set; }
    }

    public class ProcessMetrics
    {
        public int Pid { get; set; }
        public int? ParentPid { get; set; }
        public string Name { get; set; } = "";
        public double CpuPercent { get; set; }
        public ulong WorkingSetBytes { get; set; }
        public double DiskReadBytesPerSecond { get; set; }
        public double DiskWriteBytesPerSecond { get; set; }
        public double? NetworkBytesPerSecond { get; set; }
        public int TcpConnectionCount { get; set; }
        public int UdpConnectionCount { get; set; }
        public int NetworkConnectionCount => TcpConnectionCount + UdpConnectionCount;
        public double? GpuPercent { get; set; }
        public string? CommandLine { get; set; }
        public DateTime? StartTime { get; set; }
        public string? ExecutablePath { get; set; }
        public bool IsAccessDenied { get; set; }
    }

    public class ProcessSnapshot
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public SystemMetrics System { get; set; } = new();
        public List<ProcessMetrics> Processes { get; set; } = new();
    }
}
