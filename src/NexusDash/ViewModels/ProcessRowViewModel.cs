using NexusDash.Models;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;

namespace NexusDash.ViewModels
{
    [Flags]
    public enum ProcessRowUpdateFlags
    {
        None = 0,
        Structure = 1,
        StaticText = 2,
        LiveMetrics = 4
    }

    public sealed class ProcessRowViewModel : ReactiveObject
    {
        private static readonly Dictionary<byte[], Bitmap?> IconBitmapCache = new();
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
            var name = string.Format(CultureInfo.CurrentCulture, "{0} ({1})", title, count);
            if (string.Equals(_metrics.Name, name, StringComparison.Ordinal) &&
                string.Equals(_metrics.RawName, title, StringComparison.Ordinal))
            {
                return;
            }

            _metrics.Name = name;
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

        public ProcessRowUpdateFlags Update(ProcessMetrics metrics)
        {
            var old = _metrics;
            var oldCpuText = CpuText;
            var oldMemoryText = MemoryText;
            var oldDiskText = DiskText;
            var oldNetworkText = NetworkText;
            var oldGpuText = GpuText;
            var oldPublisherText = PublisherText;
            var oldStartTimeText = StartTimeText;
            var parentChanged = old.ParentPid != metrics.ParentPid;
            var nameChanged = !string.Equals(old.Name, metrics.Name, StringComparison.Ordinal);
            var rawNameChanged = !string.Equals(old.RawName, metrics.RawName, StringComparison.Ordinal);
            var publisherChanged = !string.Equals(old.Publisher, metrics.Publisher, StringComparison.Ordinal);
            var categoryChanged = old.Category != metrics.Category;
            var commandLineChanged = !string.Equals(old.CommandLine, metrics.CommandLine, StringComparison.Ordinal);
            var executablePathChanged = !string.Equals(old.ExecutablePath, metrics.ExecutablePath, StringComparison.Ordinal);
            var startTimeChanged = old.StartTime != metrics.StartTime;
            var accessDeniedChanged = old.IsAccessDenied != metrics.IsAccessDenied;
            var flags = ProcessRowUpdateFlags.None;

            if (parentChanged || categoryChanged)
            {
                flags |= ProcessRowUpdateFlags.Structure;
            }

            if (parentChanged ||
                nameChanged ||
                rawNameChanged ||
                publisherChanged ||
                categoryChanged ||
                commandLineChanged ||
                executablePathChanged ||
                startTimeChanged ||
                accessDeniedChanged)
            {
                flags |= ProcessRowUpdateFlags.StaticText;
            }

            _metrics = metrics;

            if (parentChanged)
            {
                this.RaisePropertyChanged(nameof(ParentPid));
                this.RaisePropertyChanged(nameof(ParentPidText));
            }

            if (nameChanged)
            {
                this.RaisePropertyChanged(nameof(Name));
            }

            if (rawNameChanged)
            {
                this.RaisePropertyChanged(nameof(RawName));
            }

            if (publisherChanged)
            {
                this.RaisePropertyChanged(nameof(Publisher));
            }

            if (categoryChanged)
            {
                this.RaisePropertyChanged(nameof(Category));
            }

            UpdateIcon(metrics.IconBytes);
            if (categoryChanged)
            {
                RaiseCategoryIconProperties();
            }

            if (!string.Equals(oldCpuText, CpuText, StringComparison.Ordinal))
            {
                this.RaisePropertyChanged(nameof(CpuPercent));
                this.RaisePropertyChanged(nameof(CpuText));
                flags |= ProcessRowUpdateFlags.LiveMetrics;
            }

            if (!string.Equals(oldMemoryText, MemoryText, StringComparison.Ordinal))
            {
                this.RaisePropertyChanged(nameof(WorkingSetBytes));
                this.RaisePropertyChanged(nameof(MemoryText));
                flags |= ProcessRowUpdateFlags.LiveMetrics;
            }

            if (!string.Equals(oldDiskText, DiskText, StringComparison.Ordinal))
            {
                this.RaisePropertyChanged(nameof(DiskBytesPerSecond));
                this.RaisePropertyChanged(nameof(DiskText));
                flags |= ProcessRowUpdateFlags.LiveMetrics;
            }

            if (old.TcpConnectionCount != metrics.TcpConnectionCount ||
                old.UdpConnectionCount != metrics.UdpConnectionCount ||
                !string.Equals(oldNetworkText, NetworkText, StringComparison.Ordinal))
            {
                this.RaisePropertyChanged(nameof(TcpConnectionCount));
                this.RaisePropertyChanged(nameof(UdpConnectionCount));
                this.RaisePropertyChanged(nameof(NetworkConnectionCount));
                this.RaisePropertyChanged(nameof(NetworkText));
                flags |= ProcessRowUpdateFlags.LiveMetrics;
            }

            if (old.GpuPercent != metrics.GpuPercent ||
                !string.Equals(oldGpuText, GpuText, StringComparison.Ordinal))
            {
                this.RaisePropertyChanged(nameof(GpuPercent));
                this.RaisePropertyChanged(nameof(GpuText));
                flags |= ProcessRowUpdateFlags.LiveMetrics;
            }

            if (publisherChanged ||
                !string.Equals(oldPublisherText, PublisherText, StringComparison.Ordinal))
            {
                this.RaisePropertyChanged(nameof(PublisherText));
            }

            if (executablePathChanged)
            {
                this.RaisePropertyChanged(nameof(ExecutablePath));
            }

            if (commandLineChanged)
            {
                this.RaisePropertyChanged(nameof(CommandLine));
            }

            if (startTimeChanged ||
                !string.Equals(oldStartTimeText, StartTimeText, StringComparison.Ordinal))
            {
                this.RaisePropertyChanged(nameof(StartTime));
                this.RaisePropertyChanged(nameof(StartTimeText));
            }

            if (accessDeniedChanged)
            {
                this.RaisePropertyChanged(nameof(IsAccessDenied));
            }

            return flags;
        }

        public void RefreshLocalizedText(string unavailableText)
        {
            if (string.Equals(_unavailableText, unavailableText, StringComparison.Ordinal))
            {
                return;
            }

            _unavailableText = unavailableText;
            this.RaisePropertyChanged(nameof(PublisherText));
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
                   (Publisher?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (ExecutablePath?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (CommandLine?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
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
            RaiseCategoryIconProperties();
        }

        private void RaiseCategoryIconProperties()
        {
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
                if (IconBitmapCache.TryGetValue(iconBytes, out var cachedBitmap))
                {
                    return cachedBitmap;
                }

                var bitmap = new Bitmap(new MemoryStream(iconBytes));
                IconBitmapCache[iconBytes] = bitmap;
                return bitmap;
            }
            catch
            {
                IconBitmapCache[iconBytes] = null;
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
