using AtomUI.Theme.Language;
using Avalonia;
using Avalonia.Threading;
using AtomUI;
using AtomUI.Controls;
using CodeWF.EventBus;
using Lang.Avalonia;
using NexusDash.Controls.Models;
using NexusDash.Models;
using NexusDash.Services;
using ReactiveUI;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NexusDash.ViewModels
{
    public sealed class MainWindowViewModel : ReactiveObject, IDisposable
    {
        private static readonly IBrush DarkProcessRowPrimaryTextBrush = new SolidColorBrush(Color.Parse("#f4f7fb"));
        private static readonly IBrush DarkProcessRowSecondaryTextBrush = new SolidColorBrush(Color.Parse("#9aa6b5"));
        private static readonly IBrush DarkProcessRowDividerBrush = new SolidColorBrush(Color.Parse("#27303c"));
        private static readonly IBrush LightProcessRowPrimaryTextBrush = new SolidColorBrush(Color.Parse("#18202c"));
        private static readonly IBrush LightProcessRowSecondaryTextBrush = new SolidColorBrush(Color.Parse("#5d6878"));
        private static readonly IBrush LightProcessRowDividerBrush = new SolidColorBrush(Color.Parse("#e4e9f2"));
        private static readonly IBrush DarkDialogSurfaceBrush = new SolidColorBrush(Color.Parse("#1b2028"));
        private static readonly IBrush DarkDialogInsetBrush = new SolidColorBrush(Color.Parse("#242b36"));
        private static readonly IBrush LightDialogSurfaceBrush = new SolidColorBrush(Color.Parse("#ffffff"));
        private static readonly IBrush LightDialogInsetBrush = new SolidColorBrush(Color.Parse("#f2f5fa"));

        public const string ProcessColumnPid = "pid";
        public const string ProcessColumnParentPid = "parentPid";
        public const string ProcessColumnName = "name";
        public const string ProcessColumnPublisher = "publisher";
        public const string ProcessColumnCpu = "cpu";
        public const string ProcessColumnMemory = "memory";
        public const string ProcessColumnDisk = "disk";
        public const string ProcessColumnNetwork = "network";
        public const string ProcessColumnGpu = "gpu";

        private readonly SystemMonitorService _systemMonitorService = new();
        private readonly ProcessTelemetryService _processTelemetryService = new();
        private readonly ProcessNetworkConnectionService _processNetworkConnectionService = new();
        private readonly CancellationTokenSource _refreshCancellation = new();
        private readonly IEventBus _processEventBus;
        private readonly Dictionary<int, ProcessRowViewModel> _rowCache = new();
        private readonly HashSet<int> _expandedPids = new();
        private readonly HashSet<int> _collapsedPids = new();
        private readonly List<ProcessRowViewModel> _rootRows = new();
        private readonly ProcessRowViewModel _applicationGroupRow;
        private readonly ProcessRowViewModel _backgroundGroupRow;
        private readonly ProcessRowViewModel _windowsGroupRow;
        private readonly Dictionary<string, ProcessColumnOptionViewModel> _processColumnOptions = new(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<ProcessRowViewModel> _pendingTerminationRows = [];
        private IReadOnlyList<ProcessRowViewModel> _selectedRows = [];
        private bool _isUpdatingLanguageOptions;
        private string _selectedCultureName = "zh-CN";
        private string _searchQuery = "";
        private bool _isDarkTheme = true;
        private bool _pendingTerminationEntireProcessTree;
        private bool _isEndProcessConfirmationVisible;
        private double _cpuUsage;
        private double _memoryUsage;
        private double _diskBytesPerSecond;
        private double _networkBytesPerSecond;
        private string _memoryUsedText = "0 B";
        private string _memoryTotalText = "0 B";
        private string _statusMessage = "";
        private string _topCpuProcessText = "";
        private string _topMemoryProcessText = "";
        private string _topDiskProcessText = "";
        private string _topNetworkProcessText = "";
        private int _processTotalCount;
        private ProcessRowViewModel? _selectedProcess;
        private LanguageOption? _selectedLanguage;
        private IReadOnlyList<TreemapItem> _treemapProcesses = [];
        private IReadOnlyList<ProcessNetworkConnection> _networkConnections = [];
        private IReadOnlyList<ProcessNetworkConnection> _selectedProcessNetworkConnections = [];
        private Task<IReadOnlyList<ProcessNetworkConnection>>? _networkRefreshTask;
        private IReadOnlyList<double> _cpuHistory = [];
        private IReadOnlyList<double> _memoryHistory = [];
        private IReadOnlyList<double> _diskHistory = [];
        private IReadOnlyList<double> _networkHistory = [];
        private bool _isRefreshPaused;
        private int _refreshIntervalSeconds = 1;

        public MainWindowViewModel()
            : this(EventBus.Default, new ProcessListViewModel(EventBus.Default))
        {
        }

        public MainWindowViewModel(IEventBus processEventBus, ProcessListViewModel processList)
        {
            _processEventBus = processEventBus;
            ProcessList = processList;
            _processEventBus.Subscribe(this);

            var preferences = UserPreferencesService.Load();
            _isDarkTheme = preferences.IsDarkTheme;
            _refreshIntervalSeconds = NormalizeRefreshIntervalSeconds(preferences.RefreshIntervalSeconds);
            Application.Current?.SetDarkThemeMode(_isDarkTheme);
            InitializeLanguageOptions();
            var unavailableText = T(NexusDashL.MetricUnavailable);
            _applicationGroupRow = ProcessRowViewModel.CreateGroupHeader(
                ProcessCategory.Application,
                -1,
                unavailableText,
                HandleRowExpansionChanged);
            _backgroundGroupRow = ProcessRowViewModel.CreateGroupHeader(
                ProcessCategory.BackgroundProcess,
                -2,
                unavailableText,
                HandleRowExpansionChanged);
            _windowsGroupRow = ProcessRowViewModel.CreateGroupHeader(
                ProcessCategory.WindowsProcess,
                -3,
                unavailableText,
                HandleRowExpansionChanged);
            SetLanguage(NormalizeCulture(preferences.CultureName), showStatus: false);
            InitializeProcessColumnOptions(preferences);
            StatusMessage = T(NexusDashL.StatusRunning);
            PublishProcessListState();
            _ = RefreshLoopAsync(_refreshCancellation.Token);
        }

        public ProcessListViewModel ProcessList { get; }
        public ObservableCollection<ProcessRowViewModel> VisibleProcesses { get; } = new();
        public ObservableCollection<ProcessColumnOptionViewModel> ProcessColumns { get; } = new();
        public ObservableCollection<LanguageOption> Languages { get; } = new();

        public string WindowTitle => $"{T(NexusDashL.AppName)} - {T(NexusDashL.AppSubtitle)}";
        public string AppNameText => T(NexusDashL.AppName);
        public string AppSubtitleText => T(NexusDashL.AppSubtitle);
        public string SettingsText => T(NexusDashL.Settings);
        public string ThemeMenuText => T(NexusDashL.ThemeMenu);
        public string DarkThemeText => T(NexusDashL.DarkTheme);
        public string LightThemeText => T(NexusDashL.LightTheme);
        public string LanguageMenuText => T(NexusDashL.LanguageMenu);
        public string PauseText => T(NexusDashL.Pause);
        public string ResumeText => T(NexusDashL.Resume);
        public string SearchPlaceholderText => T(NexusDashL.SearchPlaceholder);
        public string SearchNoResultsText => string.Format(CultureInfo.CurrentCulture, T(NexusDashL.SearchNoResults), SearchQuery.Trim());
        public string EndProcessText => T(NexusDashL.EndProcess);
        public string EndProcessTreeText => T(NexusDashL.EndProcessTree);
        public string ProcessTreeText => T(NexusDashL.ProcessTree);
        public string TreemapText => T(NexusDashL.Treemap);
        public string DetailsText => T(NexusDashL.Details);
        public string HandlesText => T(NexusDashL.Handles);
        public string NetworkText => T(NexusDashL.Network);
        public string ServicesText => T(NexusDashL.Services);
        public string StartupText => T(NexusDashL.Startup);
        public string PidText => T(NexusDashL.Pid);
        public string ParentPidText => T(NexusDashL.ParentPid);
        public string ProcessNameText => T(NexusDashL.ProcessName);
        public string PublisherText => T(NexusDashL.Publisher);
        public string CpuText => T(NexusDashL.Cpu);
        public string MemoryText => T(NexusDashL.Memory);
        public string DiskText => T(NexusDashL.Disk);
        public string NetworkColumnText => T(NexusDashL.NetworkColumn);
        public string GpuText => T(NexusDashL.Gpu);
        public string PathText => T(NexusDashL.Path);
        public string CommandLineText => T(NexusDashL.CommandLine);
        public string StartTimeText => T(NexusDashL.StartTime);
        public string AccessLimitedText => T(NexusDashL.AccessLimited);
        public string AccessLimitedDescriptionText => T(NexusDashL.AccessLimitedDescription);
        public string NoProcessSelectedText => T(NexusDashL.NoProcessSelected);
        public string HandlesSearchPlaceholderText => T(NexusDashL.HandlesSearchPlaceholder);
        public string HandlesUnavailableText => T(NexusDashL.HandlesUnavailable);
        public string NetworkUnavailableText => T(NexusDashL.NetworkUnavailable);
        public string ServicesUnavailableText => T(NexusDashL.ServicesUnavailable);
        public string StartupUnavailableText => T(NexusDashL.StartupUnavailable);
        public string ProcessNetworkConnectionsText => T(NexusDashL.ProcessNetworkConnections);
        public string NetworkSelectProcessText => T(NexusDashL.NetworkSelectProcess);
        public string NetworkNoConnectionsText => T(NexusDashL.NetworkNoConnections);
        public string ConnectionCountText => T(NexusDashL.ConnectionCount);
        public string TcpText => T(NexusDashL.Tcp);
        public string UdpText => T(NexusDashL.Udp);
        public string ProtocolText => T(NexusDashL.Protocol);
        public string LocalEndpointText => T(NexusDashL.LocalEndpoint);
        public string RemoteEndpointText => T(NexusDashL.RemoteEndpoint);
        public string StateText => T(NexusDashL.State);
        public string LastSeenText => T(NexusDashL.LastSeen);
        public string OwnerProcessText => T(NexusDashL.OwnerProcess);
        public string ColumnVisibilityText => T(NexusDashL.ColumnVisibility);
        public string RequiredColumnText => T(NexusDashL.RequiredColumn);
        public string ProcessCountText => IsSearchActive
            ? string.Format(CultureInfo.CurrentCulture, T(NexusDashL.SearchResultCount), VisibleProcessCount, ProcessTotalCount)
            : $"{VisibleProcessCount}/{ProcessTotalCount}";
        public string SelectedCountText => string.Format(CultureInfo.CurrentCulture, T(NexusDashL.StatusSelected), SelectedProcessCount);
        public string CpuUsageText => $"{CpuUsage:F1}%";
        public string MemoryUsageText => $"{MemoryUsage:F1}%";
        public string MemorySummaryText => $"{MemoryUsedText} / {MemoryTotalText}";
        public string DiskSpeedText => ProcessRowViewModel.FormatSpeed(DiskBytesPerSecond);
        public string NetworkSpeedText => ProcessRowViewModel.FormatSpeed(NetworkBytesPerSecond);
        public string SelectedProcessNetworkSummaryText => SelectedProcess is null
            ? T(NexusDashL.NetworkSelectProcess)
            : string.Format(
                CultureInfo.CurrentCulture,
                T(NexusDashL.NetworkSelectedSummary),
                SelectedProcess.Name,
                SelectedProcessNetworkConnections.Count,
                SelectedProcessTcpConnectionCount,
                SelectedProcessUdpConnectionCount);
        public string SelectedProcessConnectionTotalText => string.Format(
            CultureInfo.CurrentCulture,
            T(NexusDashL.NetworkTotalCount),
            SelectedProcessNetworkConnections.Count);
        public string SelectedProcessTcpConnectionCountText => string.Format(
            CultureInfo.CurrentCulture,
            T(NexusDashL.TcpConnectionCount),
            SelectedProcessTcpConnectionCount);
        public string SelectedProcessUdpConnectionCountText => string.Format(
            CultureInfo.CurrentCulture,
            T(NexusDashL.UdpConnectionCount),
            SelectedProcessUdpConnectionCount);
        public string ConfirmText => T(NexusDashL.Confirm);
        public string CancelText => T(NexusDashL.Cancel);
        public string EndProcessConfirmationTitleText => T(_pendingTerminationEntireProcessTree
            ? NexusDashL.ConfirmEndProcessTreeTitle
            : NexusDashL.ConfirmEndProcessTitle);
        public string EndProcessConfirmationMessageText => string.Format(
            CultureInfo.CurrentCulture,
            T(_pendingTerminationEntireProcessTree
                ? NexusDashL.ConfirmEndProcessTreeMessage
                : NexusDashL.ConfirmEndProcessMessage),
            _pendingTerminationRows.Count);
        public string EndProcessConfirmationProcessListText => string.Join(
            Environment.NewLine,
            _pendingTerminationRows
                .Take(6)
                .Select(row => $"{row.Name} ({row.Pid})")
                .Concat(_pendingTerminationRows.Count > 6 ? [$"+{_pendingTerminationRows.Count - 6}"] : []));
        public bool HasSelectedProcesses => SelectedProcessCount > 0;
        public bool HasSelectedProcess => SelectedProcess is not null;
        public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchQuery);
        public bool HasNoVisibleProcesses => IsSearchActive && VisibleProcessCount == 0;
        public bool HasSelectedProcessAccessLimit => SelectedProcess?.IsAccessDenied == true;
        public bool HasSelectedProcessNetworkConnections => SelectedProcessNetworkConnections.Count > 0;
        public bool HasSelectedProcessWithoutNetworkConnections => HasSelectedProcess && !HasSelectedProcessNetworkConnections;
        public bool IsEndProcessConfirmationVisible
        {
            get => _isEndProcessConfirmationVisible;
            private set => this.RaiseAndSetIfChanged(ref _isEndProcessConfirmationVisible, value);
        }
        public bool IsLightTheme => !IsDarkTheme;
        public bool IsRefreshPaused
        {
            get => _isRefreshPaused;
            private set
            {
                if (SetField(ref _isRefreshPaused, value, nameof(IsRefreshPaused)))
                {
                    this.RaisePropertyChanged(nameof(IsRefreshRunning));
                }
            }
        }
        public bool IsRefreshRunning => !IsRefreshPaused;
        public int RefreshIntervalSeconds
        {
            get => _refreshIntervalSeconds;
            private set
            {
                if (SetField(ref _refreshIntervalSeconds, value, nameof(RefreshIntervalSeconds)))
                {
                    this.RaisePropertyChanged(nameof(RefreshIntervalText));
                }
            }
        }
        public string RefreshIntervalText => $"{RefreshIntervalSeconds}s";
        public IBrush ProcessRowPrimaryTextBrush => IsDarkTheme ? DarkProcessRowPrimaryTextBrush : LightProcessRowPrimaryTextBrush;
        public IBrush ProcessRowSecondaryTextBrush => IsDarkTheme ? DarkProcessRowSecondaryTextBrush : LightProcessRowSecondaryTextBrush;
        public IBrush ProcessRowDividerBrush => IsDarkTheme ? DarkProcessRowDividerBrush : LightProcessRowDividerBrush;
        public IBrush DialogSurfaceBrush => IsDarkTheme ? DarkDialogSurfaceBrush : LightDialogSurfaceBrush;
        public IBrush DialogInsetBrush => IsDarkTheme ? DarkDialogInsetBrush : LightDialogInsetBrush;
        public bool IsSimplifiedChinese => string.Equals(_selectedCultureName, "zh-CN", StringComparison.OrdinalIgnoreCase);
        public bool IsTraditionalChinese => string.Equals(_selectedCultureName, "zh-Hant", StringComparison.OrdinalIgnoreCase);
        public bool IsEnglish => string.Equals(_selectedCultureName, "en-US", StringComparison.OrdinalIgnoreCase);
        public bool IsJapanese => string.Equals(_selectedCultureName, "ja-JP", StringComparison.OrdinalIgnoreCase);
        public int SelectedProcessCount => _selectedRows.Count;
        private int VisibleProcessCount => VisibleProcesses.Count(static row => !row.IsGroupHeader);
        public int SelectedProcessTcpConnectionCount => SelectedProcessNetworkConnections.Count(static connection => connection.Protocol == "TCP");
        public int SelectedProcessUdpConnectionCount => SelectedProcessNetworkConnections.Count(static connection => connection.Protocol == "UDP");

        public LanguageOption? SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetField(ref _selectedLanguage, value, nameof(SelectedLanguage)) &&
                    value is not null &&
                    !_isUpdatingLanguageOptions)
                {
                    SetLanguage(value.CultureName);
                }
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetField(ref _searchQuery, value ?? "", nameof(SearchQuery)))
                {
                    RebuildVisibleProcesses();
                }
            }
        }

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (SetField(ref _isDarkTheme, value, nameof(IsDarkTheme)))
                {
                    Application.Current?.SetDarkThemeMode(value);
                    UserPreferencesService.Update(preferences => preferences.IsDarkTheme = value);
                    this.RaisePropertyChanged(nameof(IsLightTheme));
                    this.RaisePropertyChanged(nameof(ProcessRowPrimaryTextBrush));
                    this.RaisePropertyChanged(nameof(ProcessRowSecondaryTextBrush));
                    this.RaisePropertyChanged(nameof(ProcessRowDividerBrush));
                    this.RaisePropertyChanged(nameof(DialogSurfaceBrush));
                    this.RaisePropertyChanged(nameof(DialogInsetBrush));
                    PublishProcessListState();
                }
            }
        }

        public double CpuUsage
        {
            get => _cpuUsage;
            set
            {
                if (SetField(ref _cpuUsage, value, nameof(CpuUsage)))
                {
                    this.RaisePropertyChanged(nameof(CpuUsageText));
                }
            }
        }

        public double MemoryUsage
        {
            get => _memoryUsage;
            set
            {
                if (SetField(ref _memoryUsage, value, nameof(MemoryUsage)))
                {
                    this.RaisePropertyChanged(nameof(MemoryUsageText));
                }
            }
        }

        public double DiskBytesPerSecond
        {
            get => _diskBytesPerSecond;
            set
            {
                if (SetField(ref _diskBytesPerSecond, value, nameof(DiskBytesPerSecond)))
                {
                    this.RaisePropertyChanged(nameof(DiskSpeedText));
                }
            }
        }

        public double NetworkBytesPerSecond
        {
            get => _networkBytesPerSecond;
            set
            {
                if (SetField(ref _networkBytesPerSecond, value, nameof(NetworkBytesPerSecond)))
                {
                    this.RaisePropertyChanged(nameof(NetworkSpeedText));
                }
            }
        }

        public string MemoryUsedText
        {
            get => _memoryUsedText;
            set
            {
                if (SetField(ref _memoryUsedText, value, nameof(MemoryUsedText)))
                {
                    this.RaisePropertyChanged(nameof(MemorySummaryText));
                }
            }
        }

        public string MemoryTotalText
        {
            get => _memoryTotalText;
            set
            {
                if (SetField(ref _memoryTotalText, value, nameof(MemoryTotalText)))
                {
                    this.RaisePropertyChanged(nameof(MemorySummaryText));
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public string TopCpuProcessText
        {
            get => _topCpuProcessText;
            private set => this.RaiseAndSetIfChanged(ref _topCpuProcessText, value);
        }

        public string TopMemoryProcessText
        {
            get => _topMemoryProcessText;
            private set => this.RaiseAndSetIfChanged(ref _topMemoryProcessText, value);
        }

        public string TopDiskProcessText
        {
            get => _topDiskProcessText;
            private set => this.RaiseAndSetIfChanged(ref _topDiskProcessText, value);
        }

        public string TopNetworkProcessText
        {
            get => _topNetworkProcessText;
            private set => this.RaiseAndSetIfChanged(ref _topNetworkProcessText, value);
        }

        public int ProcessTotalCount
        {
            get => _processTotalCount;
            set
            {
                if (SetField(ref _processTotalCount, value, nameof(ProcessTotalCount)))
                {
                    this.RaisePropertyChanged(nameof(ProcessCountText));
                }
            }
        }

        public ProcessRowViewModel? SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                if (SetField(ref _selectedProcess, value, nameof(SelectedProcess)))
                {
                    this.RaisePropertyChanged(nameof(HasSelectedProcess));
                    this.RaisePropertyChanged(nameof(HasSelectedProcessAccessLimit));
                    RefreshSelectedNetworkConnections();
                }
            }
        }

        public IReadOnlyList<TreemapItem> TreemapProcesses
        {
            get => _treemapProcesses;
            set => this.RaiseAndSetIfChanged(ref _treemapProcesses, value);
        }

        public IReadOnlyList<ProcessNetworkConnection> SelectedProcessNetworkConnections
        {
            get => _selectedProcessNetworkConnections;
            set
            {
                if (SetField(ref _selectedProcessNetworkConnections, value, nameof(SelectedProcessNetworkConnections)))
                {
                    this.RaisePropertyChanged(nameof(HasSelectedProcessNetworkConnections));
                    this.RaisePropertyChanged(nameof(HasSelectedProcessWithoutNetworkConnections));
                    this.RaisePropertyChanged(nameof(SelectedProcessTcpConnectionCount));
                    this.RaisePropertyChanged(nameof(SelectedProcessUdpConnectionCount));
                    this.RaisePropertyChanged(nameof(SelectedProcessNetworkSummaryText));
                    this.RaisePropertyChanged(nameof(SelectedProcessConnectionTotalText));
                    this.RaisePropertyChanged(nameof(SelectedProcessTcpConnectionCountText));
                    this.RaisePropertyChanged(nameof(SelectedProcessUdpConnectionCountText));
                }
            }
        }

        public IReadOnlyList<double> CpuHistory
        {
            get => _cpuHistory;
            set => this.RaiseAndSetIfChanged(ref _cpuHistory, value);
        }

        public IReadOnlyList<double> MemoryHistory
        {
            get => _memoryHistory;
            set => this.RaiseAndSetIfChanged(ref _memoryHistory, value);
        }

        public IReadOnlyList<double> DiskHistory
        {
            get => _diskHistory;
            set => this.RaiseAndSetIfChanged(ref _diskHistory, value);
        }

        public IReadOnlyList<double> NetworkHistory
        {
            get => _networkHistory;
            set => this.RaiseAndSetIfChanged(ref _networkHistory, value);
        }

        public void SetDarkTheme()
        {
            IsDarkTheme = true;
            StatusMessage = string.Format(CultureInfo.CurrentCulture, T(NexusDashL.StatusThemeChanged), T(NexusDashL.DarkTheme));
        }

        public void SetLightTheme()
        {
            IsDarkTheme = false;
            StatusMessage = string.Format(CultureInfo.CurrentCulture, T(NexusDashL.StatusThemeChanged), T(NexusDashL.LightTheme));
        }

        public void PauseRefresh()
        {
            if (IsRefreshPaused)
            {
                return;
            }

            IsRefreshPaused = true;
            StatusMessage = T(NexusDashL.StatusPaused);
        }

        public void ResumeRefresh()
        {
            if (!IsRefreshPaused)
            {
                return;
            }

            IsRefreshPaused = false;
            StatusMessage = T(NexusDashL.StatusRunning);
        }

        public void SetRefreshIntervalSeconds(int seconds)
        {
            var normalizedSeconds = NormalizeRefreshIntervalSeconds(seconds);
            if (RefreshIntervalSeconds == normalizedSeconds)
            {
                return;
            }

            RefreshIntervalSeconds = normalizedSeconds;
            UserPreferencesService.Update(preferences => preferences.RefreshIntervalSeconds = normalizedSeconds);
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                T(NexusDashL.StatusRefreshCadenceChanged),
                RefreshIntervalText);
        }

        public void SelectSimplifiedChinese()
        {
            SetLanguage("zh-CN");
        }

        public void SelectTraditionalChinese()
        {
            SetLanguage("zh-Hant");
        }

        public void SelectEnglish()
        {
            SetLanguage("en-US");
        }

        public void SelectJapanese()
        {
            SetLanguage("ja-JP");
        }

        public void EndSelectedProcesses()
        {
            RequestEndSelectedProcesses(entireProcessTree: false);
        }

        public void EndSelectedProcessTrees()
        {
            RequestEndSelectedProcesses(entireProcessTree: true);
        }

        public void ConfirmPendingProcessTermination()
        {
            var rows = _pendingTerminationRows;
            var entireProcessTree = _pendingTerminationEntireProcessTree;
            IsEndProcessConfirmationVisible = false;
            _pendingTerminationRows = [];
            RaiseTerminationConfirmationProperties();
            _ = EndSelectedProcessesAsync(rows, entireProcessTree);
        }

        public void CancelPendingProcessTermination()
        {
            IsEndProcessConfirmationVisible = false;
            _pendingTerminationRows = [];
            RaiseTerminationConfirmationProperties();
        }

        public void SetSelectedProcesses(IEnumerable<ProcessRowViewModel> selectedRows)
        {
            _selectedRows = selectedRows.Where(static row => !row.IsGroupHeader).ToArray();
            SelectedProcess = _selectedRows.LastOrDefault();
            this.RaisePropertyChanged(nameof(SelectedProcessCount));
            this.RaisePropertyChanged(nameof(HasSelectedProcesses));
            this.RaisePropertyChanged(nameof(SelectedCountText));
            StatusMessage = SelectedCountText;
            PublishProcessListState();
        }

        public bool IsProcessColumnVisible(string key)
        {
            return !_processColumnOptions.TryGetValue(key, out var option) || option.IsVisible;
        }

        public void SetProcessColumnVisibility(string key, bool isVisible)
        {
            if (_processColumnOptions.TryGetValue(key, out var option))
            {
                option.IsVisible = isVisible;
            }
        }

        [EventHandler]
        private void HandleProcessListSelectionChanged(ProcessListSelectionChangedCommand command)
        {
            SetSelectedProcesses(command.SelectedRows);
        }

        [EventHandler]
        private void HandleProcessTerminationRequested(ProcessTerminationRequestedCommand command)
        {
            RequestEndSelectedProcesses(command.EntireProcessTree);
        }

        [EventHandler]
        private void HandleProcessColumnVisibilityChanged(ProcessColumnVisibilityChangedCommand command)
        {
            SetProcessColumnVisibility(command.Key, command.IsVisible);
        }

        private void PublishProcessListState()
        {
            _processEventBus.Publish(new ProcessListStateChangedCommand(new ProcessListState
            {
                VisibleProcesses = VisibleProcesses.ToArray(),
                ProcessColumns = ProcessColumns.ToArray(),
                SelectedProcessPid = SelectedProcess?.Pid,
                ProcessTreeText = ProcessTreeText,
                ProcessCountText = ProcessCountText,
                SearchNoResultsText = SearchNoResultsText,
                EndProcessText = EndProcessText,
                EndProcessTreeText = EndProcessTreeText,
                PidText = PidText,
                ParentPidText = ParentPidText,
                ProcessNameText = ProcessNameText,
                PublisherText = PublisherText,
                CpuText = CpuText,
                MemoryText = MemoryText,
                DiskText = DiskText,
                NetworkColumnText = NetworkColumnText,
                GpuText = GpuText,
                AccessLimitedText = AccessLimitedText,
                ColumnVisibilityText = ColumnVisibilityText,
                RequiredColumnText = RequiredColumnText,
                HasSelectedProcesses = HasSelectedProcesses,
                HasNoVisibleProcesses = HasNoVisibleProcesses,
                ProcessRowPrimaryTextBrush = ProcessRowPrimaryTextBrush,
                ProcessRowSecondaryTextBrush = ProcessRowSecondaryTextBrush
            }));
        }

        private void RequestEndSelectedProcesses(bool entireProcessTree)
        {
            var rows = _selectedRows
                .Where(static row => !row.IsGroupHeader)
                .GroupBy(static row => row.Pid)
                .Select(static group => group.First())
                .ToArray();

            if (rows.Length == 0)
            {
                return;
            }

            _pendingTerminationRows = rows;
            _pendingTerminationEntireProcessTree = entireProcessTree;
            IsEndProcessConfirmationVisible = true;
            RaiseTerminationConfirmationProperties();
        }

        private void RefreshSelectedNetworkConnections()
        {
            if (SelectedProcess is null)
            {
                SelectedProcessNetworkConnections = [];
            }
            else
            {
                SelectedProcessNetworkConnections = _networkConnections
                    .Where(connection => connection.Pid == SelectedProcess.Pid)
                    .OrderBy(static connection => connection.Protocol, StringComparer.Ordinal)
                    .ThenBy(static connection => connection.State, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(static connection => connection.LocalPort)
                    .ThenBy(static connection => connection.RemoteEndpointText, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
            }

            this.RaisePropertyChanged(nameof(SelectedProcessNetworkSummaryText));
            this.RaisePropertyChanged(nameof(SelectedProcessConnectionTotalText));
            this.RaisePropertyChanged(nameof(SelectedProcessTcpConnectionCountText));
            this.RaisePropertyChanged(nameof(SelectedProcessUdpConnectionCountText));
        }

        private async Task RefreshLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!IsRefreshPaused)
                    {
                        await RefreshAsync(cancellationToken);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(RefreshIntervalSeconds), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => StatusMessage = exception.Message);
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
        }

        private async Task RefreshAsync(CancellationToken cancellationToken)
        {
            var systemTask = _systemMonitorService.GetMetricsAsync();
            var processTask = _processTelemetryService.GetProcessesAsync();
            var networkConnections = GetLatestNetworkConnectionsSnapshot();
            await Task.WhenAll(systemTask, processTask);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() => ApplySnapshot(systemTask.Result, processTask.Result, networkConnections));
        }

        private IReadOnlyList<ProcessNetworkConnection> GetLatestNetworkConnectionsSnapshot()
        {
            if (_networkRefreshTask is { IsCompleted: true } completedTask)
            {
                try
                {
                    _networkConnections = completedTask.IsCompletedSuccessfully ? completedTask.Result : [];
                }
                catch
                {
                    _networkConnections = [];
                }

                _networkRefreshTask = null;
            }

            _networkRefreshTask ??= _processNetworkConnectionService.GetConnectionsAsync();
            return _networkConnections;
        }

        private void ApplySnapshot(
            SystemMetrics systemMetrics,
            IReadOnlyList<ProcessMetrics> processes,
            IReadOnlyList<ProcessNetworkConnection> networkConnections)
        {
            var processSnapshot = processes.ToArray();
            var enrichedNetworkConnections = EnrichNetworkConnections(networkConnections, processSnapshot);
            ApplyProcessNetworkCounts(processSnapshot, enrichedNetworkConnections);

            var cpuUsage = Math.Min(100, processes.Sum(static p => p.CpuPercent));
            var diskBytesPerSecond = processes.Sum(static p => p.DiskReadBytesPerSecond + p.DiskWriteBytesPerSecond);
            var networkBytesPerSecond = systemMetrics.Network.UploadSpeed + systemMetrics.Network.DownloadSpeed;

            CpuUsage = cpuUsage;
            MemoryUsage = systemMetrics.Memory.UsagePercentage;
            DiskBytesPerSecond = diskBytesPerSecond;
            NetworkBytesPerSecond = networkBytesPerSecond;
            MemoryUsedText = ProcessRowViewModel.FormatBytes(systemMetrics.Memory.UsedBytes);
            MemoryTotalText = ProcessRowViewModel.FormatBytes(systemMetrics.Memory.TotalBytes);
            UpdateTopProcessInsights(processSnapshot);

            CpuHistory = AppendHistory(CpuHistory, cpuUsage);
            MemoryHistory = AppendHistory(MemoryHistory, MemoryUsage);
            DiskHistory = AppendHistory(DiskHistory, Math.Min(100, diskBytesPerSecond / 1024 / 1024));
            NetworkHistory = AppendHistory(NetworkHistory, Math.Min(100, networkBytesPerSecond / 1024 / 1024));

            _networkConnections = enrichedNetworkConnections;
            RefreshSelectedNetworkConnections();

            ProcessTotalCount = processSnapshot.Length;
            RebuildProcessTree(processSnapshot);
        }

        private static IReadOnlyList<ProcessNetworkConnection> EnrichNetworkConnections(
            IReadOnlyList<ProcessNetworkConnection> networkConnections,
            IReadOnlyList<ProcessMetrics> processes)
        {
            var namesByPid = processes.ToDictionary(static process => process.Pid, static process => process.Name);
            return networkConnections
                .Select(connection =>
                {
                    if (connection.Pid is not { } pid ||
                        !string.IsNullOrWhiteSpace(connection.ProcessName) ||
                        !namesByPid.TryGetValue(pid, out var processName))
                    {
                        return connection;
                    }

                    return new ProcessNetworkConnection
                    {
                        Protocol = connection.Protocol,
                        Pid = connection.Pid,
                        ProcessName = processName,
                        LocalAddress = connection.LocalAddress,
                        LocalPort = connection.LocalPort,
                        RemoteAddress = connection.RemoteAddress,
                        RemotePort = connection.RemotePort,
                        State = connection.State,
                        Timestamp = connection.Timestamp
                    };
                })
                .ToArray();
        }

        private static void ApplyProcessNetworkCounts(
            IReadOnlyList<ProcessMetrics> processes,
            IReadOnlyList<ProcessNetworkConnection> networkConnections)
        {
            var countsByPid = networkConnections
                .Where(static connection => connection.Pid is not null)
                .GroupBy(static connection => connection.Pid!.Value)
                .ToDictionary(
                    static group => group.Key,
                    static group => new
                    {
                        Tcp = group.Count(static connection => connection.Protocol == "TCP"),
                        Udp = group.Count(static connection => connection.Protocol == "UDP")
                    });

            foreach (var process in processes)
            {
                if (countsByPid.TryGetValue(process.Pid, out var counts))
                {
                    process.TcpConnectionCount = counts.Tcp;
                    process.UdpConnectionCount = counts.Udp;
                }
            }
        }

        private void UpdateTopProcessInsights(IReadOnlyList<ProcessMetrics> processes)
        {
            var topCpu = processes
                .OrderByDescending(static process => process.CpuPercent)
                .ThenByDescending(static process => process.WorkingSetBytes)
                .FirstOrDefault();
            var topMemory = processes
                .OrderByDescending(static process => process.WorkingSetBytes)
                .FirstOrDefault();
            var topDisk = processes
                .Select(static process => new
                {
                    Process = process,
                    BytesPerSecond = process.DiskReadBytesPerSecond + process.DiskWriteBytesPerSecond
                })
                .OrderByDescending(static item => item.BytesPerSecond)
                .FirstOrDefault();
            var topNetwork = processes
                .OrderByDescending(static process => process.NetworkConnectionCount)
                .ThenByDescending(static process => process.TcpConnectionCount)
                .FirstOrDefault();

            TopCpuProcessText = topCpu is null
                ? ""
                : string.Format(
                    CultureInfo.CurrentCulture,
                    T(NexusDashL.TopCpuProcess),
                    topCpu.Name,
                    topCpu.CpuPercent);
            TopMemoryProcessText = topMemory is null
                ? ""
                : string.Format(
                    CultureInfo.CurrentCulture,
                    T(NexusDashL.TopMemoryProcess),
                    topMemory.Name,
                    ProcessRowViewModel.FormatBytes(topMemory.WorkingSetBytes));
            TopDiskProcessText = topDisk is null || topDisk.BytesPerSecond <= 0
                ? T(NexusDashL.NoDiskActivity)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    T(NexusDashL.TopDiskProcess),
                    topDisk.Process.Name,
                    ProcessRowViewModel.FormatSpeed(topDisk.BytesPerSecond));
            TopNetworkProcessText = topNetwork is null || topNetwork.NetworkConnectionCount <= 0
                ? T(NexusDashL.NoNetworkConnectionsVisible)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    T(NexusDashL.TopNetworkConnections),
                    topNetwork.Name,
                    topNetwork.NetworkConnectionCount);
        }

        private void RebuildProcessTree(IReadOnlyList<ProcessMetrics> processes)
        {
            var activePids = processes.Select(static p => p.Pid).ToHashSet();
            foreach (var stalePid in _rowCache.Keys.Where(pid => !activePids.Contains(pid)).ToArray())
            {
                _rowCache.Remove(stalePid);
                _expandedPids.Remove(stalePid);
                _collapsedPids.Remove(stalePid);
            }

            var unavailableText = T(NexusDashL.MetricUnavailable);
            foreach (var process in processes)
            {
                if (_rowCache.TryGetValue(process.Pid, out var row))
                {
                    row.Update(process);
                    row.RefreshLocalizedText(unavailableText);
                    row.Parent = null;
                    row.Children.Clear();
                }
                else
                {
                    row = new ProcessRowViewModel(process, unavailableText, HandleRowExpansionChanged);
                    _rowCache[process.Pid] = row;
                }
            }

            _rootRows.Clear();
            foreach (var row in _rowCache.Values)
            {
                if (row.ParentPid is { } parentPid &&
                    parentPid != row.Pid &&
                    _rowCache.TryGetValue(parentPid, out var parent))
                {
                    row.Parent = parent;
                    parent.Children.Add(row);
                }
                else
                {
                    row.Parent = null;
                    _rootRows.Add(row);
                }
            }

            SortAndAssignDepth(_rootRows, 1);
            RebuildVisibleProcesses();
            TreemapProcesses = _rowCache.Values
                .OrderByDescending(static row => row.WorkingSetBytes)
                .Take(32)
                .Select(static row => new TreemapItem(row.Name, row.MemoryText, row.WorkingSetBytes))
                .ToArray();

            if (SelectedProcess is not null && _rowCache.TryGetValue(SelectedProcess.Pid, out var refreshedSelection))
            {
                SelectedProcess = refreshedSelection;
            }
        }

        private void SortAndAssignDepth(IList<ProcessRowViewModel> rows, int depth)
        {
            var sorted = rows
                .OrderBy(static row => row.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(static row => row.Pid)
                .ToArray();

            rows.Clear();
            foreach (var row in sorted)
            {
                rows.Add(row);
                row.Depth = depth;
                row.RefreshChildrenState();

                if (depth <= 1 && row.HasChildren && !_collapsedPids.Contains(row.Pid))
                {
                    _expandedPids.Add(row.Pid);
                }

                row.SetExpandedFromTree(_expandedPids.Contains(row.Pid));
                SortAndAssignDepth(row.Children, depth + 1);
            }
        }

        private void HandleRowExpansionChanged(ProcessRowViewModel row, bool isExpanded)
        {
            if (isExpanded)
            {
                _expandedPids.Add(row.Pid);
                _collapsedPids.Remove(row.Pid);
            }
            else
            {
                _expandedPids.Remove(row.Pid);
                _collapsedPids.Add(row.Pid);
            }

            RebuildVisibleProcesses();
        }

        private void RebuildVisibleProcesses()
        {
            var visible = new List<ProcessRowViewModel>();
            var query = SearchQuery.Trim();

            foreach (var category in GetProcessCategoryOrder())
            {
                var groupRow = GetGroupRow(category);
                groupRow.Children.Clear();

                if (query.Length == 0)
                {
                    var roots = GetCategoryRoots(category)
                        .ToArray();

                    if (roots.Length == 0)
                    {
                        continue;
                    }

                    foreach (var root in roots)
                    {
                        root.Parent = groupRow;
                        groupRow.Children.Add(root);
                    }

                    groupRow.Depth = 0;
                    groupRow.UpdateGroupHeader(GetProcessCategoryText(category), CountProcessRows(roots, category));
                    groupRow.RefreshChildrenState();
                    AppendExpandedCategoryRows(groupRow, category, visible, 0);
                }
                else
                {
                    var matches = new List<ProcessRowViewModel>();
                    foreach (var root in _rootRows)
                    {
                        CollectMatchingRows(root, category, query, matches);
                    }

                    if (matches.Count == 0)
                    {
                        continue;
                    }

                    foreach (var match in matches)
                    {
                        match.Parent = groupRow;
                        groupRow.Children.Add(match);
                    }

                    groupRow.Depth = 0;
                    groupRow.UpdateGroupHeader(GetProcessCategoryText(category), matches.Count);
                    groupRow.RefreshChildrenState();
                    groupRow.SetDisplayDepth(0);
                    visible.Add(groupRow);

                    if (!groupRow.IsExpanded)
                    {
                        continue;
                    }

                    foreach (var match in matches)
                    {
                        match.SetDisplayDepth(1);
                        visible.Add(match);
                    }
                }
            }

            ReplaceCollection(VisibleProcesses, visible);
            RaiseProcessVisibilityProperties();
            PublishProcessListState();
        }

        private void RaiseProcessVisibilityProperties()
        {
            this.RaisePropertyChanged(nameof(IsSearchActive));
            this.RaisePropertyChanged(nameof(HasNoVisibleProcesses));
            this.RaisePropertyChanged(nameof(SearchNoResultsText));
            this.RaisePropertyChanged(nameof(ProcessCountText));
        }

        private static void AppendExpandedRows(ProcessRowViewModel row, IList<ProcessRowViewModel> visible)
        {
            row.SetDisplayDepth(row.Depth);
            visible.Add(row);
            if (!row.IsExpanded)
            {
                return;
            }

            foreach (var child in row.Children)
            {
                AppendExpandedRows(child, visible);
            }
        }

        private static void AppendMatchingRows(ProcessRowViewModel row, string query, IList<ProcessRowViewModel> visible)
        {
            if (row.Matches(query))
            {
                row.SetDisplayDepth(0);
                visible.Add(row);
            }

            foreach (var child in row.Children)
            {
                AppendMatchingRows(child, query, visible);
            }
        }

        private static void AppendExpandedCategoryRows(
            ProcessRowViewModel row,
            ProcessCategory category,
            IList<ProcessRowViewModel> visible,
            int displayDepth)
        {
            row.SetDisplayDepth(displayDepth);
            visible.Add(row);
            if (!row.IsExpanded)
            {
                return;
            }

            foreach (var child in row.Children.Where(child => child.IsGroupHeader || child.Category == category))
            {
                AppendExpandedCategoryRows(child, category, visible, displayDepth + 1);
            }
        }

        private static void CollectMatchingRows(
            ProcessRowViewModel row,
            ProcessCategory category,
            string query,
            IList<ProcessRowViewModel> matches)
        {
            if (row.Category == category && row.Matches(query))
            {
                matches.Add(row);
            }

            foreach (var child in row.Children)
            {
                CollectMatchingRows(child, category, query, matches);
            }
        }

        private static int CountProcessRows(IEnumerable<ProcessRowViewModel> rows, ProcessCategory category)
        {
            var count = 0;
            foreach (var row in rows)
            {
                if (!row.IsGroupHeader && row.Category == category)
                {
                    count++;
                }

                count += CountProcessRows(row.Children.Where(child => child.Category == category), category);
            }

            return count;
        }

        private IEnumerable<ProcessRowViewModel> GetCategoryRoots(ProcessCategory category)
        {
            return _rowCache.Values
                .Where(row => !row.IsGroupHeader &&
                              row.Category == category &&
                              (row.Parent is null || row.Parent.Category != category))
                .OrderBy(static row => row.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(static row => row.Pid);
        }

        private ProcessRowViewModel GetGroupRow(ProcessCategory category)
        {
            return category switch
            {
                ProcessCategory.Application => _applicationGroupRow,
                ProcessCategory.WindowsProcess => _windowsGroupRow,
                _ => _backgroundGroupRow
            };
        }

        private string GetProcessCategoryText(ProcessCategory category)
        {
            return category switch
            {
                ProcessCategory.Application => T(NexusDashL.ProcessGroupApplications),
                ProcessCategory.WindowsProcess => T(NexusDashL.ProcessGroupWindows),
                _ => T(NexusDashL.ProcessGroupBackground)
            };
        }

        private static IReadOnlyList<ProcessCategory> GetProcessCategoryOrder()
        {
            return
            [
                ProcessCategory.Application,
                ProcessCategory.BackgroundProcess,
                ProcessCategory.WindowsProcess
            ];
        }

        private static void ReplaceCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
        {
            var index = 0;
            while (index < source.Count)
            {
                if (index >= target.Count)
                {
                    target.Add(source[index]);
                }
                else if (!EqualityComparer<T>.Default.Equals(target[index], source[index]))
                {
                    target.RemoveAt(index);
                    target.Insert(index, source[index]);
                }

                index++;
            }

            while (target.Count > source.Count)
            {
                target.RemoveAt(target.Count - 1);
            }
        }

        private async Task EndSelectedProcessesAsync(IReadOnlyList<ProcessRowViewModel> rows, bool entireProcessTree)
        {
            var pids = rows
                .Where(static row => !row.IsGroupHeader)
                .Select(static row => row.Pid)
                .Distinct()
                .ToArray();
            if (pids.Length == 0)
            {
                return;
            }

            try
            {
                await Task.Run(() =>
                {
                    foreach (var pid in pids)
                    {
                        ProcessTelemetryService.EndProcess(pid, entireProcessTree);
                    }
                });

                StatusMessage = string.Format(CultureInfo.CurrentCulture, T(NexusDashL.StatusEnded), pids.Length);
            }
            catch (Exception exception)
            {
                StatusMessage = string.Format(CultureInfo.CurrentCulture, T(NexusDashL.StatusEndFailed), exception.Message);
            }
        }

        private static IReadOnlyList<double> AppendHistory(IReadOnlyList<double> history, double value)
        {
            return history
                .Concat([Math.Clamp(value, 0, 100)])
                .TakeLast(60)
                .ToArray();
        }

        private void RaiseTerminationConfirmationProperties()
        {
            this.RaisePropertyChanged(nameof(EndProcessConfirmationTitleText));
            this.RaisePropertyChanged(nameof(EndProcessConfirmationMessageText));
            this.RaisePropertyChanged(nameof(EndProcessConfirmationProcessListText));
        }

        private void InitializeProcessColumnOptions(UserPreferences preferences)
        {
            var visibility = preferences.ProcessColumnVisibility ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            AddProcessColumnOption(ProcessColumnPid, PidText, isRequired: true, visibility);
            AddProcessColumnOption(ProcessColumnParentPid, ParentPidText, isRequired: true, visibility);
            AddProcessColumnOption(ProcessColumnName, ProcessNameText, isRequired: true, visibility);
            AddProcessColumnOption(ProcessColumnPublisher, PublisherText, isRequired: false, visibility);
            AddProcessColumnOption(ProcessColumnCpu, CpuText, isRequired: false, visibility);
            AddProcessColumnOption(ProcessColumnMemory, MemoryText, isRequired: false, visibility);
            AddProcessColumnOption(ProcessColumnDisk, DiskText, isRequired: false, visibility);
            AddProcessColumnOption(ProcessColumnNetwork, NetworkColumnText, isRequired: false, visibility);
            AddProcessColumnOption(ProcessColumnGpu, GpuText, isRequired: false, visibility);
        }

        private void AddProcessColumnOption(
            string key,
            string header,
            bool isRequired,
            IReadOnlyDictionary<string, bool> visibility)
        {
            var isVisible = isRequired || !visibility.TryGetValue(key, out var savedVisible) || savedVisible;
            var option = new ProcessColumnOptionViewModel(
                key,
                header,
                isRequired,
                isVisible,
                HandleProcessColumnVisibilityChanged);
            _processColumnOptions[key] = option;
            ProcessColumns.Add(option);
        }

        private void HandleProcessColumnVisibilityChanged(ProcessColumnOptionViewModel option)
        {
            if (option.IsRequired)
            {
                return;
            }

            UserPreferencesService.Update(preferences =>
            {
                preferences.ProcessColumnVisibility ??= new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                preferences.ProcessColumnVisibility[option.Key] = option.IsVisible;
            });

            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                T(option.IsVisible ? NexusDashL.StatusColumnShown : NexusDashL.StatusColumnHidden),
                option.Header);
            PublishProcessListState();
        }

        private void RefreshProcessColumnHeaders()
        {
            RefreshProcessColumnHeader(ProcessColumnPid, PidText);
            RefreshProcessColumnHeader(ProcessColumnParentPid, ParentPidText);
            RefreshProcessColumnHeader(ProcessColumnName, ProcessNameText);
            RefreshProcessColumnHeader(ProcessColumnPublisher, PublisherText);
            RefreshProcessColumnHeader(ProcessColumnCpu, CpuText);
            RefreshProcessColumnHeader(ProcessColumnMemory, MemoryText);
            RefreshProcessColumnHeader(ProcessColumnDisk, DiskText);
            RefreshProcessColumnHeader(ProcessColumnNetwork, NetworkColumnText);
            RefreshProcessColumnHeader(ProcessColumnGpu, GpuText);
        }

        private void RefreshProcessColumnHeader(string key, string header)
        {
            if (_processColumnOptions.TryGetValue(key, out var option))
            {
                option.RefreshHeader(header);
            }
        }

        private void InitializeLanguageOptions()
        {
            Languages.Clear();
            Languages.Add(new LanguageOption("zh-CN", T(NexusDashL.SimplifiedChinese)));
            Languages.Add(new LanguageOption("zh-Hant", T(NexusDashL.TraditionalChinese)));
            Languages.Add(new LanguageOption("en-US", T(NexusDashL.English)));
            Languages.Add(new LanguageOption("ja-JP", T(NexusDashL.Japanese)));
        }

        private void SetLanguage(string cultureName, bool showStatus = true)
        {
            var normalizedCultureName = NormalizeCulture(cultureName);
            if (string.Equals(_selectedCultureName, normalizedCultureName, StringComparison.OrdinalIgnoreCase) && showStatus)
            {
                return;
            }

            var culture = CultureInfo.GetCultureInfo(normalizedCultureName);
            _selectedCultureName = culture.Name;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            I18nManager.Instance.Culture = culture;
            UserPreferencesService.Update(preferences => preferences.CultureName = culture.Name);
            Application.Current?.SetLanguageVariant(
                culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
                    ? LanguageVariant.zh_CN
                    : LanguageVariant.en_US);

            RefreshLanguageOptions();
            RefreshLocalizedProperties();

            foreach (var row in _rowCache.Values)
            {
                row.RefreshLocalizedText(T(NexusDashL.MetricUnavailable));
            }

            _applicationGroupRow.RefreshLocalizedText(T(NexusDashL.MetricUnavailable));
            _backgroundGroupRow.RefreshLocalizedText(T(NexusDashL.MetricUnavailable));
            _windowsGroupRow.RefreshLocalizedText(T(NexusDashL.MetricUnavailable));
            RebuildVisibleProcesses();

            if (showStatus)
            {
                StatusMessage = string.Format(CultureInfo.CurrentCulture, T(NexusDashL.StatusLanguageChanged), SelectedLanguage?.DisplayName ?? culture.Name);
            }
        }

        private void RefreshLanguageOptions()
        {
            _isUpdatingLanguageOptions = true;
            try
            {
                var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["zh-CN"] = T(NexusDashL.SimplifiedChinese),
                    ["zh-Hant"] = T(NexusDashL.TraditionalChinese),
                    ["en-US"] = T(NexusDashL.English),
                    ["ja-JP"] = T(NexusDashL.Japanese)
                };

                foreach (var language in Languages)
                {
                    if (labels.TryGetValue(language.CultureName, out var label))
                    {
                        language.DisplayName = label;
                    }
                }

                _selectedLanguage = Languages.FirstOrDefault(language =>
                    string.Equals(language.CultureName, _selectedCultureName, StringComparison.OrdinalIgnoreCase));
                this.RaisePropertyChanged(nameof(SelectedLanguage));
                this.RaisePropertyChanged(nameof(IsSimplifiedChinese));
                this.RaisePropertyChanged(nameof(IsTraditionalChinese));
                this.RaisePropertyChanged(nameof(IsEnglish));
                this.RaisePropertyChanged(nameof(IsJapanese));
            }
            finally
            {
                _isUpdatingLanguageOptions = false;
            }
        }

        private void RefreshLocalizedProperties()
        {
            this.RaisePropertyChanged(nameof(WindowTitle));
            this.RaisePropertyChanged(nameof(AppNameText));
            this.RaisePropertyChanged(nameof(AppSubtitleText));
            this.RaisePropertyChanged(nameof(SettingsText));
            this.RaisePropertyChanged(nameof(ThemeMenuText));
            this.RaisePropertyChanged(nameof(DarkThemeText));
            this.RaisePropertyChanged(nameof(LightThemeText));
            this.RaisePropertyChanged(nameof(LanguageMenuText));
            this.RaisePropertyChanged(nameof(PauseText));
            this.RaisePropertyChanged(nameof(ResumeText));
            this.RaisePropertyChanged(nameof(RefreshIntervalText));
            this.RaisePropertyChanged(nameof(SearchPlaceholderText));
            this.RaisePropertyChanged(nameof(SearchNoResultsText));
            this.RaisePropertyChanged(nameof(EndProcessText));
            this.RaisePropertyChanged(nameof(EndProcessTreeText));
            this.RaisePropertyChanged(nameof(ProcessTreeText));
            this.RaisePropertyChanged(nameof(TreemapText));
            this.RaisePropertyChanged(nameof(DetailsText));
            this.RaisePropertyChanged(nameof(HandlesText));
            this.RaisePropertyChanged(nameof(NetworkText));
            this.RaisePropertyChanged(nameof(ServicesText));
            this.RaisePropertyChanged(nameof(StartupText));
            this.RaisePropertyChanged(nameof(PidText));
            this.RaisePropertyChanged(nameof(ParentPidText));
            this.RaisePropertyChanged(nameof(ProcessNameText));
            this.RaisePropertyChanged(nameof(PublisherText));
            this.RaisePropertyChanged(nameof(CpuText));
            this.RaisePropertyChanged(nameof(MemoryText));
            this.RaisePropertyChanged(nameof(DiskText));
            this.RaisePropertyChanged(nameof(NetworkColumnText));
            this.RaisePropertyChanged(nameof(GpuText));
            this.RaisePropertyChanged(nameof(PathText));
            this.RaisePropertyChanged(nameof(CommandLineText));
            this.RaisePropertyChanged(nameof(StartTimeText));
            this.RaisePropertyChanged(nameof(AccessLimitedText));
            this.RaisePropertyChanged(nameof(AccessLimitedDescriptionText));
            this.RaisePropertyChanged(nameof(NoProcessSelectedText));
            this.RaisePropertyChanged(nameof(HandlesSearchPlaceholderText));
            this.RaisePropertyChanged(nameof(HandlesUnavailableText));
            this.RaisePropertyChanged(nameof(NetworkUnavailableText));
            this.RaisePropertyChanged(nameof(ServicesUnavailableText));
            this.RaisePropertyChanged(nameof(StartupUnavailableText));
            this.RaisePropertyChanged(nameof(ProcessNetworkConnectionsText));
            this.RaisePropertyChanged(nameof(NetworkSelectProcessText));
            this.RaisePropertyChanged(nameof(NetworkNoConnectionsText));
            this.RaisePropertyChanged(nameof(ConnectionCountText));
            this.RaisePropertyChanged(nameof(TcpText));
            this.RaisePropertyChanged(nameof(UdpText));
            this.RaisePropertyChanged(nameof(ProtocolText));
            this.RaisePropertyChanged(nameof(LocalEndpointText));
            this.RaisePropertyChanged(nameof(RemoteEndpointText));
            this.RaisePropertyChanged(nameof(StateText));
            this.RaisePropertyChanged(nameof(LastSeenText));
            this.RaisePropertyChanged(nameof(OwnerProcessText));
            this.RaisePropertyChanged(nameof(ColumnVisibilityText));
            this.RaisePropertyChanged(nameof(RequiredColumnText));
            this.RaisePropertyChanged(nameof(ProcessCountText));
            this.RaisePropertyChanged(nameof(SelectedProcessNetworkSummaryText));
            this.RaisePropertyChanged(nameof(SelectedProcessConnectionTotalText));
            this.RaisePropertyChanged(nameof(SelectedProcessTcpConnectionCountText));
            this.RaisePropertyChanged(nameof(SelectedProcessUdpConnectionCountText));
            UpdateTopProcessInsights(_rowCache.Values
                .Where(static row => !row.IsGroupHeader)
                .Select(static row => new ProcessMetrics
                {
                    Pid = row.Pid,
                    Name = row.Name,
                    CpuPercent = row.CpuPercent,
                    WorkingSetBytes = row.WorkingSetBytes,
                    DiskReadBytesPerSecond = row.DiskBytesPerSecond,
                    TcpConnectionCount = row.TcpConnectionCount,
                    UdpConnectionCount = row.UdpConnectionCount
                })
                .ToArray());
            this.RaisePropertyChanged(nameof(ConfirmText));
            this.RaisePropertyChanged(nameof(CancelText));
            RaiseTerminationConfirmationProperties();
            RefreshProcessColumnHeaders();
            this.RaisePropertyChanged(nameof(SelectedCountText));
            PublishProcessListState();
        }

        private static string NormalizeCulture(string cultureName)
        {
            if (cultureName.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
                cultureName.Equals("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                cultureName.Equals("zh-HK", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-Hant";
            }

            if (cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-CN";
            }

            if (cultureName.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            {
                return "ja-JP";
            }

            if (cultureName.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                return "en-US";
            }

            return "zh-CN";
        }

        private static int NormalizeRefreshIntervalSeconds(int seconds)
        {
            return seconds switch
            {
                2 => 2,
                5 => 5,
                _ => 1
            };
        }

        private static string T(string key)
        {
            return I18nManager.Instance.GetResource(key) ?? key;
        }

        private bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            this.RaiseAndSetIfChanged(ref field, value, propertyName);
            return true;
        }

        public void Dispose()
        {
            _refreshCancellation.Cancel();
            _refreshCancellation.Dispose();
            _processEventBus.Unsubscribe(this);
            ProcessList.Dispose();
            _systemMonitorService.Dispose();
        }
    }
}
