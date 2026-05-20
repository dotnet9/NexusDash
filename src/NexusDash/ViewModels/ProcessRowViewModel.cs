using NexusDash.Models;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Globalization;

namespace NexusDash.ViewModels
{
    public sealed class ProcessRowViewModel : ReactiveObject
    {
        private readonly Action<ProcessRowViewModel, bool> _expandedChanged;
        private ProcessMetrics _metrics;
        private int _depth;
        private bool _isExpanded;
        private string _unavailableText;

        public ProcessRowViewModel(
            ProcessMetrics metrics,
            string unavailableText,
            Action<ProcessRowViewModel, bool> expandedChanged)
        {
            _metrics = metrics;
            _unavailableText = unavailableText;
            _expandedChanged = expandedChanged;
        }

        public ObservableCollection<ProcessRowViewModel> Children { get; } = new();

        public int Pid => _metrics.Pid;
        public int? ParentPid => _metrics.ParentPid;
        public string Name => _metrics.Name;
        public double CpuPercent => _metrics.CpuPercent;
        public ulong WorkingSetBytes => _metrics.WorkingSetBytes;
        public string? ExecutablePath => _metrics.ExecutablePath;
        public string? CommandLine => _metrics.CommandLine;
        public DateTime? StartTime => _metrics.StartTime;
        public bool IsAccessDenied => _metrics.IsAccessDenied;
        public bool HasChildren => Children.Count > 0;
        public string ParentPidText => ParentPid?.ToString(CultureInfo.CurrentCulture) ?? "";
        public string CpuText => $"{CpuPercent:F1}%";
        public string MemoryText => FormatBytes(WorkingSetBytes);
        public string DiskText => FormatSpeed(_metrics.DiskReadBytesPerSecond + _metrics.DiskWriteBytesPerSecond);
        public string NetworkText => _metrics.NetworkBytesPerSecond is { } value ? FormatSpeed(value) : _unavailableText;
        public string GpuText => _metrics.GpuPercent is { } value ? $"{value:F1}%" : _unavailableText;
        public string StartTimeText => StartTime?.ToString("G", CultureInfo.CurrentCulture) ?? "";
        public string ExpanderGlyph => IsExpanded ? "v" : ">";

        public int Depth
        {
            get => _depth;
            set => this.RaiseAndSetIfChanged(ref _depth, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                {
                    return;
                }

                this.RaiseAndSetIfChanged(ref _isExpanded, value);
                this.RaisePropertyChanged(nameof(ExpanderGlyph));
                _expandedChanged(this, value);
            }
        }

        public void SetExpandedFromTree(bool value)
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            this.RaisePropertyChanged(nameof(IsExpanded));
            this.RaisePropertyChanged(nameof(ExpanderGlyph));
        }

        public void Update(ProcessMetrics metrics)
        {
            _metrics = metrics;
            this.RaisePropertyChanged(nameof(ParentPid));
            this.RaisePropertyChanged(nameof(ParentPidText));
            this.RaisePropertyChanged(nameof(Name));
            this.RaisePropertyChanged(nameof(CpuPercent));
            this.RaisePropertyChanged(nameof(CpuText));
            this.RaisePropertyChanged(nameof(WorkingSetBytes));
            this.RaisePropertyChanged(nameof(MemoryText));
            this.RaisePropertyChanged(nameof(DiskText));
            this.RaisePropertyChanged(nameof(NetworkText));
            this.RaisePropertyChanged(nameof(GpuText));
            this.RaisePropertyChanged(nameof(ExecutablePath));
            this.RaisePropertyChanged(nameof(CommandLine));
            this.RaisePropertyChanged(nameof(StartTime));
            this.RaisePropertyChanged(nameof(StartTimeText));
            this.RaisePropertyChanged(nameof(IsAccessDenied));
        }

        public void RefreshLocalizedText(string unavailableText)
        {
            _unavailableText = unavailableText;
            this.RaisePropertyChanged(nameof(NetworkText));
            this.RaisePropertyChanged(nameof(GpuText));
        }

        public void RefreshChildrenState()
        {
            this.RaisePropertyChanged(nameof(HasChildren));
            this.RaisePropertyChanged(nameof(ExpanderGlyph));
        }

        public bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return Pid.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   (ExecutablePath?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (CommandLine?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        public static string FormatBytes(double bytes)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
            var value = Math.Max(bytes, 0);
            var unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return unit == 0 ? $"{value:F0} {units[unit]}" : $"{value:F2} {units[unit]}";
        }

        public static string FormatSpeed(double bytesPerSecond)
        {
            return $"{FormatBytes(bytesPerSecond)}/s";
        }
    }
}
