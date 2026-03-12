using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NexusDash.Models;
using NexusDash.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace NexusDash.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject, IDisposable
    {
        private readonly SystemMonitorService _monitorService;
        private readonly Timer _animationTimer;

        [ObservableProperty]
        private double _cpuUsage;

        [ObservableProperty]
        private double _cpuRotationAngle;

        [ObservableProperty]
        private int _cpuCoreCount;

        [ObservableProperty]
        private double _cpuTemperature;

        [ObservableProperty]
        private double _memoryUsage;

        [ObservableProperty]
        private string _memoryUsedText = "0 GB";

        [ObservableProperty]
        private string _memoryTotalText = "0 GB";

        [ObservableProperty]
        private ObservableCollection<DiskMetrics> _disks = new();

        [ObservableProperty]
        private double _networkUploadSpeed;

        [ObservableProperty]
        private double _networkDownloadSpeed;

        [ObservableProperty]
        private int _networkConnections;

        [ObservableProperty]
        private bool _isRunning = true;

        [ObservableProperty]
        private string _statusMessage = "系统监控运行中...";

        private double _targetRotationSpeed = 0.5;
        private double _currentRotationSpeed = 0.5;

        public MainWindowViewModel()
        {
            _monitorService = new SystemMonitorService();
            _monitorService.MetricsUpdated += OnMetricsUpdated;

            _animationTimer = new Timer(16); // ~60 FPS
            _animationTimer.Elapsed += OnAnimationTick;
            _animationTimer.AutoReset = true;
            _animationTimer.Start();

            _monitorService.Start();
        }

        private void OnMetricsUpdated(object? sender, SystemMetrics metrics)
        {
            _ = Task.Run(() =>
            {
                CpuUsage = metrics.Cpu.TotalUsage;
                CpuCoreCount = metrics.Cpu.CoreCount;
                CpuTemperature = metrics.Cpu.Temperature;

                MemoryUsage = metrics.Memory.UsagePercentage;
                MemoryUsedText = FormatBytes(metrics.Memory.UsedBytes);
                MemoryTotalText = FormatBytes(metrics.Memory.TotalBytes);

                Disks.Clear();
                foreach (var disk in metrics.Disks)
                {
                    Disks.Add(disk);
                }

                NetworkUploadSpeed = metrics.Network.UploadSpeed;
                NetworkDownloadSpeed = metrics.Network.DownloadSpeed;
                NetworkConnections = metrics.Network.ConnectionCount;

                // Update rotation speed based on CPU usage
                // 0% = 0.5 deg/frame, 100% = 4.0 deg/frame
                _targetRotationSpeed = 0.5 + (metrics.Cpu.TotalUsage / 100.0) * 3.5;
            });
        }

        private void OnAnimationTick(object? sender, ElapsedEventArgs e)
        {
            if (!IsRunning) return;

            // Smooth rotation speed transition
            _currentRotationSpeed += (_targetRotationSpeed - _currentRotationSpeed) * 0.1;

            // Update rotation angle
            CpuRotationAngle += _currentRotationSpeed;
            if (CpuRotationAngle >= 360)
            {
                CpuRotationAngle -= 360;
            }
        }

        [RelayCommand]
        private void TogglePause()
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
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:F2} {sizes[order]}";
        }

        public void Dispose()
        {
            _animationTimer?.Dispose();
            _monitorService?.Dispose();
        }
    }
}
