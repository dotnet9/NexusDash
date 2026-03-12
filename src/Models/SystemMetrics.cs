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
}
