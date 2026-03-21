using NexusDash.Models;
using NexusDash.Services;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace NexusDash.ViewModels
{
    public partial class MainWindowViewModel : ReactiveObject, IDisposable
    {
        private readonly SystemMonitorService _monitorService;

        public double CpuUsage
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public int CpuCoreCount
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public double CpuTemperature
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public double MemoryUsage
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public string MemoryUsedText
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        } = "0 GB";

        public string MemoryTotalText
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        } = "0 GB";

        public ObservableCollection<DiskMetrics> Disks
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        } = new();

        public double NetworkUploadSpeed
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public double NetworkDownloadSpeed
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public string NetworkUploadSpeedText
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        } = "0 KB/s";

        public string NetworkDownloadSpeedText
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        } = "0 KB/s";

        public int NetworkConnections
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        }

        public bool IsRunning
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        } = true;

        public string StatusMessage
        {
            get;
            set => this.RaiseAndSetIfChanged(ref field, value);
        } = "系统监控运行中...";

        public MainWindowViewModel()
        {
            _monitorService = new SystemMonitorService();
            _monitorService.MetricsUpdated += OnMetricsUpdated;
            _monitorService.Start();
        }

        private void OnMetricsUpdated(object? sender, SystemMetrics metrics)
        {
            _ = Task.Run(() =>
            {
                CpuUsage = metrics.Cpu.TotalUsage;
                CpuCoreCount = metrics.Cpu.CoreCount;
                CpuTemperature = metrics.Cpu.Temperature;

                var memoryUsage = metrics.Memory.UsagePercentage;
                var memoryUsed = FormatBytes(metrics.Memory.UsedBytes);
                var memoryTotal = FormatBytes(metrics.Memory.TotalBytes);

                System.Diagnostics.Debug.WriteLine($"ViewModel: MemoryUsage={memoryUsage:F2}%, Used={memoryUsed}, Total={memoryTotal}");

                MemoryUsage = memoryUsage;
                MemoryUsedText = memoryUsed;
                MemoryTotalText = memoryTotal;

                // 更新磁盘列表，根据Name匹配更新
                foreach (var newDisk in metrics.Disks)
                {
                    var existingDisk = Disks.FirstOrDefault(d => d.Name == newDisk.Name);
                    if (existingDisk != null)
                    {
                        // 更新现有磁盘数据
                        existingDisk.DriveLetter = newDisk.DriveLetter;
                        existingDisk.TotalBytes = newDisk.TotalBytes;
                        existingDisk.UsedBytes = newDisk.UsedBytes;
                        existingDisk.FreeBytes = newDisk.FreeBytes;
                        existingDisk.ReadSpeed = newDisk.ReadSpeed;
                        existingDisk.WriteSpeed = newDisk.WriteSpeed;
                    }
                    else
                    {
                        // 添加新磁盘
                        Disks.Add(newDisk);
                    }
                }
                // 移除不再存在的磁盘
                for (int i = Disks.Count - 1; i >= 0; i--)
                {
                    if (!metrics.Disks.Any(d => d.Name == Disks[i].Name))
                    {
                        Disks.RemoveAt(i);
                    }
                }

                NetworkUploadSpeed = metrics.Network.UploadSpeed;
                NetworkDownloadSpeed = metrics.Network.DownloadSpeed;
                NetworkUploadSpeedText = FormatSpeed(metrics.Network.UploadSpeed);
                NetworkDownloadSpeedText = FormatSpeed(metrics.Network.DownloadSpeed);
                NetworkConnections = metrics.Network.ConnectionCount;
            });
        }

        public void RaiseTogglePauseHandler()
        {
            IsRunning = !IsRunning;
            StatusMessage = IsRunning ? "系统监控运行中..." : "系统监控已暂停";

            if (IsRunning)
            {
                _monitorService.Start();
            }
            else
            {
                _monitorService.Stop();
            }
        }

        private static string FormatBytes(ulong bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = (double)bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:F2} {sizes[order]}";
        }

        private static string FormatSpeed(double bytesPerSecond)
        {
            string[] sizes = { "B/s", "KB/s", "MB/s", "GB/s", "TB/s" };
            double len = bytesPerSecond;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            string format = order == 0 ? "F0" : "F2";
            return $"{len.ToString(format)} {sizes[order]}";
        }

        public void Dispose()
        {
            _monitorService?.Dispose();
        }
    }
}
