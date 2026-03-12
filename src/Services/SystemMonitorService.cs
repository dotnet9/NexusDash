using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using NexusDash.Models;
using Timer = System.Timers.Timer;

namespace NexusDash.Services
{
    public class SystemMonitorService : IDisposable
    {
        private readonly Timer _updateTimer;
        private readonly PerformanceCounter _cpuCounter;
        private readonly List<PerformanceCounter> _coreCounters = new();
        private readonly PerformanceCounter _memoryCounter;
        private readonly Dictionary<string, (PerformanceCounter readCounter, PerformanceCounter writeCounter)> _diskCounters = new();
        private PerformanceCounter _networkSentCounter;
        private PerformanceCounter _networkReceivedCounter;
        
        private readonly Queue<SystemMetrics> _history = new();
        private const int MaxHistorySize = 60;
        
        private ulong _lastNetworkSent;
        private ulong _lastNetworkReceived;
        private DateTime _lastNetworkTime;

        public event EventHandler<SystemMetrics>? MetricsUpdated;

        public SystemMetrics CurrentMetrics { get; private set; } = new();
        public IReadOnlyCollection<SystemMetrics> History => _history.ToList().AsReadOnly();

        public SystemMonitorService()
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue();

            var processorCount = Environment.ProcessorCount;
            for (int i = 0; i < processorCount; i++)
            {
                var coreCounter = new PerformanceCounter("Processor", "% Processor Time", i.ToString());
                coreCounter.NextValue();
                _coreCounters.Add(coreCounter);
            }

            _memoryCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
            _memoryCounter.NextValue();

            InitializeDiskCounters();
            InitializeNetworkCounters();

            _updateTimer = new Timer(1000);
            _updateTimer.Elapsed += OnTimerElapsed;
            _updateTimer.AutoReset = true;
        }

        private void InitializeDiskCounters()
        {
            try
            {
                var driveInfos = System.IO.DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
                    .ToList();

                foreach (var drive in driveInfos)
                {
                    var driveName = drive.Name.TrimEnd('\\', ':', '/');
                    try
                    {
                        var readCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
                        var writeCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
                        readCounter.NextValue();
                        writeCounter.NextValue();
                        _diskCounters[driveName] = (readCounter, writeCounter);
                    }
                    catch
                    {
                        // Ignore drives that don't support performance counters
                    }
                }
            }
            catch
            {
                // Ignore disk counter initialization errors
            }
        }

        private void InitializeNetworkCounters()
        {
            try
            {
                var category = new PerformanceCounterCategory("Network Interface");
                var instanceNames = category.GetInstanceNames();
                
                if (instanceNames.Length > 0)
                {
                    var instanceName = instanceNames.FirstOrDefault(n => !n.ToLower().Contains("loopback")) ?? instanceNames[0];
                    _networkSentCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", instanceName);
                    _networkReceivedCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", instanceName);
                    _networkSentCounter.NextValue();
                    _networkReceivedCounter.NextValue();
                    _lastNetworkTime = DateTime.Now;
                }
            }
            catch
            {
                // Ignore network counter initialization errors
            }
        }

        public void Start()
        {
            _updateTimer.Start();
            _ = UpdateMetricsAsync();
        }

        public void Stop()
        {
            _updateTimer.Stop();
        }

        private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            _ = UpdateMetricsAsync();
        }

        private async Task UpdateMetricsAsync()
        {
            try
            {
                var metrics = new SystemMetrics();

                // CPU Metrics
                await Task.Run(() =>
                {
                    var cpuUsage = _cpuCounter.NextValue();
                    metrics.Cpu.TotalUsage = Math.Min(100, Math.Max(0, cpuUsage));
                    metrics.Cpu.CoreCount = _coreCounters.Count;
                    
                    foreach (var coreCounter in _coreCounters)
                    {
                        var coreUsage = coreCounter.NextValue();
                        metrics.Cpu.CoreUsages.Add(Math.Min(100, Math.Max(0, coreUsage)));
                    }

                    // Try to get temperature (Windows only, may require specific hardware support)
                    try
                    {
                        using var searcher = new System.Management.ManagementObjectSearcher("root\\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
                        var temps = new List<double>();
                        foreach (var obj in searcher.Get())
                        {
                            var temp = Convert.ToDouble(obj["CurrentTemperature"]) / 10.0 - 273.15;
                            temps.Add(temp);
                        }
                        if (temps.Count > 0)
                        {
                            metrics.Cpu.Temperature = temps.Average();
                        }
                    }
                    catch
                    {
                        metrics.Cpu.Temperature = 0;
                    }
                });

                // Memory Metrics
                await Task.Run(() =>
                {
                    var memoryUsage = _memoryCounter.NextValue();
                    
                    // Get memory info using GC and Environment
                    var totalMemory = GC.GetTotalMemory(false);
                    var workingSet = Environment.WorkingSet;
                    
                    // Use PerformanceCounter for more accurate memory info
                    try
                    {
                        using var availableBytesCounter = new PerformanceCounter("Memory", "Available Bytes");
                        using var totalVisibleMemoryCounter = new PerformanceCounter("Memory", "Total Visible Memory Size");
                        
                        var availableBytes = (ulong)availableBytesCounter.NextValue();
                        var totalBytes = (ulong)totalVisibleMemoryCounter.NextValue() * 1024; // Convert from KB to bytes
                        
                        metrics.Memory.TotalBytes = totalBytes;
                        metrics.Memory.AvailableBytes = availableBytes;
                        metrics.Memory.UsedBytes = totalBytes - availableBytes;
                    }
                    catch
                    {
                        // Fallback to GC info
                        metrics.Memory.TotalBytes = (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                        metrics.Memory.UsedBytes = (ulong)workingSet;
                        metrics.Memory.AvailableBytes = metrics.Memory.TotalBytes - metrics.Memory.UsedBytes;
                    }
                });

                // Disk Metrics
                await Task.Run(() =>
                {
                    var driveInfos = System.IO.DriveInfo.GetDrives()
                        .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
                        .ToList();

                    foreach (var drive in driveInfos)
                    {
                        var driveMetrics = new DiskMetrics
                        {
                            Name = drive.VolumeLabel,
                            DriveLetter = drive.Name.TrimEnd('\\', ':', '/'),
                            TotalBytes = (ulong)drive.TotalSize,
                            FreeBytes = (ulong)drive.AvailableFreeSpace,
                            UsedBytes = (ulong)(drive.TotalSize - drive.AvailableFreeSpace)
                        };

                        if (_diskCounters.TryGetValue(driveMetrics.DriveLetter, out var counters))
                        {
                            driveMetrics.ReadSpeed = counters.readCounter.NextValue();
                            driveMetrics.WriteSpeed = counters.writeCounter.NextValue();
                        }

                        metrics.Disks.Add(driveMetrics);
                    }
                });

                // Network Metrics
                await Task.Run(() =>
                {
                    if (_networkSentCounter != null && _networkReceivedCounter != null)
                    {
                        var currentTime = DateTime.Now;
                        var timeDiff = (currentTime - _lastNetworkTime).TotalSeconds;

                        if (timeDiff > 0)
                        {
                            var sent = (ulong)_networkSentCounter.NextValue();
                            var received = (ulong)_networkReceivedCounter.NextValue();

                            metrics.Network.UploadSpeed = sent / 1024; // Convert to KB/s
                            metrics.Network.DownloadSpeed = received / 1024; // Convert to KB/s
                            metrics.Network.TotalBytesUploaded += sent;
                            metrics.Network.TotalBytesDownloaded += received;

                            _lastNetworkSent = sent;
                            _lastNetworkReceived = received;
                            _lastNetworkTime = currentTime;
                        }
                    }

                    // Get connection count
                    try
                    {
                        var tcpConnections = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties()
                            .GetActiveTcpConnections();
                        metrics.Network.ConnectionCount = tcpConnections.Length;
                    }
                    catch
                    {
                        metrics.Network.ConnectionCount = 0;
                    }
                });

                // Update history
                _history.Enqueue(metrics);
                while (_history.Count > MaxHistorySize)
                {
                    _history.Dequeue();
                }

                CurrentMetrics = metrics;
                MetricsUpdated?.Invoke(this, metrics);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating metrics: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _updateTimer?.Dispose();
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
            
            foreach (var counter in _coreCounters)
            {
                counter?.Dispose();
            }

            foreach (var (readCounter, writeCounter) in _diskCounters.Values)
            {
                readCounter?.Dispose();
                writeCounter?.Dispose();
            }

            _networkSentCounter?.Dispose();
            _networkReceivedCounter?.Dispose();
        }
    }
}
