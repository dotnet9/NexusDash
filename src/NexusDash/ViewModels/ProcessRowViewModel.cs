using NexusDash.Models;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;

namespace NexusDash.ViewModels
{
    public sealed class ProcessRowViewModel : ReactiveObject
    {
        private readonly Action<ProcessRowViewModel, bool> _expandedChanged;
        private ProcessMetrics _metrics;
        private byte[]? _iconBytes;
        private Bitmap? _icon;
        private int _depth;
        private int _displayDepth;
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
            UpdateIcon(metrics.IconBytes);
        }

        private ProcessRowViewModel(
            ProcessMetrics metrics,
            string unavailableText,
            Action<ProcessRowViewModel, bool> expandedChanged,
            bool isGroupHeader)
            : this(metrics, unavailableText, expandedChanged)
        {
            IsGroupHeader = isGroupHeader;
            _isExpanded = true;
        }

        public ObservableCollection<ProcessRowViewModel> Children { get; } = new();

        internal ProcessRowViewModel? Parent { get; set; }

        public bool IsGroupHeader { get; }
        public bool IsProcessRow => !IsGroupHeader;
        public int Pid => _metrics.Pid;
        public int? ParentPid => _metrics.ParentPid;
        public string Name => _metrics.Name;
        public string RawName => _metrics.RawName;
        public string? Publisher => _metrics.Publisher;
        public ProcessCategory Category => _metrics.Category;
        public Bitmap? Icon => _icon;
        public bool HasIcon => _icon is not null && !IsGroupHeader;
        public bool HasCategoryIcon => IsGroupHeader || !HasIcon;
        public bool ShowsApplicationIcon => HasCategoryIcon && Category == ProcessCategory.Application;
        public bool ShowsBackgroundIcon => HasCategoryIcon && Category == ProcessCategory.BackgroundProcess;
        public bool ShowsWindowsIcon => HasCategoryIcon && Category == ProcessCategory.WindowsProcess;
        public FontWeight NameFontWeight => IsGroupHeader ? FontWeight.SemiBold : FontWeight.Normal;
        public string PidCellText => IsGroupHeader ? "" : Pid.ToString(CultureInfo.CurrentCulture);
        public double CpuPercent => _metrics.CpuPercent;
        public ulong WorkingSetBytes => _metrics.WorkingSetBytes;
        public double DiskBytesPerSecond => _metrics.DiskReadBytesPerSecond + _metrics.DiskWriteBytesPerSecond;
        public double? GpuPercent => _metrics.GpuPercent;
        public int TcpConnectionCount => _metrics.TcpConnectionCount;
        public int UdpConnectionCount => _metrics.UdpConnectionCount;
        public int NetworkConnectionCount => _metrics.NetworkConnectionCount;
        public string? ExecutablePath => _metrics.ExecutablePath;
        public string? CommandLine => _metrics.CommandLine;
        public DateTime? StartTime => _metrics.StartTime;
        public bool IsAccessDenied => _metrics.IsAccessDenied;
        public bool HasChildren => Children.Count > 0;
        public string ParentPidText => IsGroupHeader ? "" : ParentPid?.ToString(CultureInfo.CurrentCulture) ?? "";
        public string PublisherText => IsGroupHeader ? "" : string.IsNullOrWhiteSpace(Publisher) ? _unavailableText : Publisher;
        public string CpuText => IsGroupHeader ? "" : $"{CpuPercent:F1}%";
        public string MemoryText => IsGroupHeader ? "" : FormatBytes(WorkingSetBytes);
        public string DiskText => IsGroupHeader ? "" : FormatSpeed(_metrics.DiskReadBytesPerSecond + _metrics.DiskWriteBytesPerSecond);
        public string NetworkText => NetworkConnectionCount > 0
            ? string.Format(CultureInfo.CurrentCulture, "{0}/{1}", TcpConnectionCount, UdpConnectionCount)
            : IsGroupHeader ? "" : "0";
        public string GpuText => IsGroupHeader ? "" : _metrics.GpuPercent is { } value ? $"{value:F1}%" : _unavailableText;
        public string StartTimeText => IsGroupHeader ? "" : StartTime?.ToString("G", CultureInfo.CurrentCulture) ?? "";
        public string ExpanderGlyph => IsExpanded ? "v" : ">";

        public static ProcessRowViewModel CreateGroupHeader(
            ProcessCategory category,
            int pid,
            string unavailableText,
            Action<ProcessRowViewModel, bool> expandedChanged)
        {
            return new ProcessRowViewModel(
                new ProcessMetrics
                {
                    Pid = pid,
                    Name = "",
                    RawName = "",
                    Category = category
                },
                unavailableText,
                expandedChanged,
                isGroupHeader: true);
        }

        public void UpdateGroupHeader(string title, int count)
        {
            _metrics.Name = string.Format(CultureInfo.CurrentCulture, "{0} ({1})", title, count);
            _metrics.RawName = title;
            this.RaisePropertyChanged(nameof(Name));
            this.RaisePropertyChanged(nameof(RawName));
            this.RaisePropertyChanged(nameof(NameFontWeight));
            this.RaisePropertyChanged(nameof(HasCategoryIcon));
            this.RaisePropertyChanged(nameof(ShowsApplicationIcon));
            this.RaisePropertyChanged(nameof(ShowsBackgroundIcon));
            this.RaisePropertyChanged(nameof(ShowsWindowsIcon));
        }

        public int Depth
        {
            get => _depth;
            set => this.RaiseAndSetIfChanged(ref _depth, value);
        }

        public int DisplayDepth
        {
            get => _displayDepth;
            private set => this.RaiseAndSetIfChanged(ref _displayDepth, value);
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

        public void ToggleExpanded()
        {
            IsExpanded = !IsExpanded;
        }

        public void SetDisplayDepth(int depth)
        {
            DisplayDepth = Math.Max(depth, 0);
        }

        public void Update(ProcessMetrics metrics)
        {
            _metrics = metrics;
            this.RaisePropertyChanged(nameof(ParentPid));
            this.RaisePropertyChanged(nameof(ParentPidText));
            this.RaisePropertyChanged(nameof(Name));
            this.RaisePropertyChanged(nameof(RawName));
            this.RaisePropertyChanged(nameof(Publisher));
            this.RaisePropertyChanged(nameof(PublisherText));
            this.RaisePropertyChanged(nameof(Category));
            this.RaisePropertyChanged(nameof(PidCellText));
            UpdateIcon(metrics.IconBytes);
            this.RaisePropertyChanged(nameof(CpuPercent));
            this.RaisePropertyChanged(nameof(CpuText));
            this.RaisePropertyChanged(nameof(WorkingSetBytes));
            this.RaisePropertyChanged(nameof(MemoryText));
            this.RaisePropertyChanged(nameof(DiskBytesPerSecond));
            this.RaisePropertyChanged(nameof(GpuPercent));
            this.RaisePropertyChanged(nameof(DiskText));
            this.RaisePropertyChanged(nameof(TcpConnectionCount));
            this.RaisePropertyChanged(nameof(UdpConnectionCount));
            this.RaisePropertyChanged(nameof(NetworkConnectionCount));
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
            this.RaisePropertyChanged(nameof(PublisherText));
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
            if (IsGroupHeader || string.IsNullOrWhiteSpace(query))
            {
                return !IsGroupHeader;
            }

            return Pid.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   RawName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   (Publisher?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private void UpdateIcon(byte[]? iconBytes)
        {
            if (ReferenceEquals(_iconBytes, iconBytes))
            {
                return;
            }

            _iconBytes = iconBytes;
            _icon = CreateBitmap(iconBytes);
            this.RaisePropertyChanged(nameof(Icon));
            this.RaisePropertyChanged(nameof(HasIcon));
            this.RaisePropertyChanged(nameof(HasCategoryIcon));
            this.RaisePropertyChanged(nameof(ShowsApplicationIcon));
            this.RaisePropertyChanged(nameof(ShowsBackgroundIcon));
            this.RaisePropertyChanged(nameof(ShowsWindowsIcon));
        }

        private static Bitmap? CreateBitmap(byte[]? iconBytes)
        {
            if (iconBytes is null || iconBytes.Length == 0)
            {
                return null;
            }

            try
            {
                return new Bitmap(new MemoryStream(iconBytes));
            }
            catch
            {
                return null;
            }
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
