using Avalonia;
using Avalonia.Threading;
using CodeWF.EventBus;
using CodeWF.Log.Core;
using Lang.Avalonia;
using NexusDash.Controls.Models;
using NexusDash.Models;
using NexusDash.Services;
using NexusDash.ViewModels.Settings;
using Prism.Commands;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NexusDash.ViewModels
{
    public sealed class MainWindowViewModel : ReactiveObject, IDisposable
    {
        public const string ProcessColumnPid = ProcessTableColumns.Pid;
        public const string ProcessColumnParentPid = ProcessTableColumns.ParentPid;
        public const string ProcessColumnName = ProcessTableColumns.Name;
        public const string ProcessColumnPublisher = ProcessTableColumns.Publisher;
        public const string ProcessColumnCpu = ProcessTableColumns.Cpu;
        public const string ProcessColumnMemory = ProcessTableColumns.Memory;
        public const string ProcessColumnDisk = ProcessTableColumns.Disk;
        public const string ProcessColumnNetwork = ProcessTableColumns.Network;
        public const string ProcessColumnGpu = ProcessTableColumns.Gpu;
        public const string ProcessFilterHasNetworkConnections = "hasNetworkConnections";
        public const string ProcessFilterHighCpu = "highCpu";
        public const string ProcessFilterUserProcesses = "userProcesses";
        public const string ProcessFilterHideSystemProcesses = "hideSystemProcesses";
        public const string ProcessManagerToolKey = "processManager";
        public const string FileSearchToolKey = "fileSearch";
        public const string HardwareInfoToolKey = "hardwareInfo";
        public const string SettingsToolKey = "settings";

        private enum ProcessTerminationRequestKind
        {
            Process,
            ProcessTree,
            Associated
        }

        private sealed record ProcessTerminationCandidateInfo(
            ProcessRowViewModel Row,
            string RelationText,
            int RelationPriority,
            int DisplayOrder,
            int TerminationOrder);

        private sealed record RefreshSnapshot(
            SystemMetrics SystemMetrics,
            IReadOnlyList<ProcessMetrics> Processes,
            IReadOnlyList<ProcessNetworkConnection> NetworkConnections,
            double CpuUsage,
            double DiskBytesPerSecond,
            double NetworkBytesPerSecond,
            bool ProcessesUpdated);

        private readonly SystemMonitorService _systemMonitorService;
        private readonly ProcessTelemetryService _processTelemetryService;
        private readonly ProcessNetworkConnectionService _processNetworkConnectionService;
        private readonly IUserPreferencesService _userPreferencesService;
        private readonly IThemeResourceService _themeResourceService;
        private readonly CancellationTokenSource _refreshCancellation = new();
        private readonly IEventBus _eventBus;
        private readonly Task _refreshLoopTask;
        private readonly Dictionary<int, ProcessRowViewModel> _rowCache = new();
        private readonly HashSet<int> _expandedPids = new();
        private readonly HashSet<int> _collapsedPids = new();
        private readonly List<ProcessRowViewModel> _rootRows = new();
        private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(2);
        private readonly TimeSpan _processSnapshotRefreshInterval = TimeSpan.FromSeconds(6);
        private readonly TimeSpan _processTreeRefreshInterval = TimeSpan.FromSeconds(6);
        private readonly TimeSpan _treemapRefreshInterval = TimeSpan.FromSeconds(6);
        private readonly ProcessRowViewModel _applicationGroupRow;
        private readonly ProcessRowViewModel _backgroundGroupRow;
        private readonly ProcessRowViewModel _windowsGroupRow;
        private readonly Dictionary<string, double> _processColumnWidths = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ProcessColumnOptionViewModel> _processColumnOptions = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _networkConnectionRefreshInterval = TimeSpan.FromSeconds(6);
        private ToolMenuNode? _processManagerNode;
        private ToolMenuNode? _fileSearchNode;
        private ToolMenuNode? _hardwareInfoNode;
        private ToolMenuNode? _settingsNode;
        private IReadOnlyList<ProcessTerminationCandidateViewModel> _pendingTerminationCandidates = [];
        private IReadOnlyList<ProcessRowViewModel> _selectedRows = [];
        private bool _isUpdatingLanguageOptions;
        private string _selectedCultureName = "";
        private string _selectedThemeKey = ThemeResourceService.DarkThemeKey;
        private string _searchQuery = "";
        private string _processSortColumnKey = ProcessColumnName;
        private ListSortDirection _processSortDirection = ListSortDirection.Ascending;
        private bool _filterHasNetworkConnections;
        private bool _filterHighCpu;
        private bool _filterUserProcesses;
        private bool _filterHideSystemProcesses;
        private bool _rememberWindowSize;
        private ProcessTerminationRequestKind _pendingTerminationKind;
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
        private string _operationLogContent = "";
        private int _processTotalCount;
        private ProcessRowViewModel? _selectedProcess;
        private ToolMenuNode? _selectedToolNode;
        private string _selectedToolKey = ProcessManagerToolKey;
        private LanguageOption? _selectedLanguage;
        private IReadOnlyList<TreemapItem> _treemapProcesses = [];
        private IReadOnlyList<ProcessNetworkConnection> _networkConnections = [];
        private IReadOnlyList<ProcessNetworkConnection> _selectedProcessNetworkConnections = [];
        private IReadOnlyList<ProcessMetrics> _latestProcessSnapshot = [];
        private IReadOnlyList<ProcessRowViewModel> _visibleProcessRowsForExport = [];
        private bool _hasBuiltProcessTree;
        private bool _hasDeferredProcessTreeUpdate;
        private bool _hasDeferredProcessLocalizationRefresh;
        private Task<IReadOnlyList<ProcessNetworkConnection>>? _networkRefreshTask;
        private IReadOnlyList<double> _cpuHistory = [];
        private IReadOnlyList<double> _memoryHistory = [];
        private IReadOnlyList<double> _diskHistory = [];
        private IReadOnlyList<double> _networkHistory = [];
        private DateTime _lastTreemapRefreshUtc = DateTime.MinValue;
        private DateTime _lastProcessSnapshotRefreshUtc = DateTime.MinValue;
        private DateTime _lastProcessTreeRefreshUtc = DateTime.MinValue;
        private DateTime _lastNetworkConnectionRefreshUtc = DateTime.MinValue;
        private bool _isRefreshPaused;
        private bool _isDisposed;

        public MainWindowViewModel(
            IEventBus eventBus,
            ProcessListViewModel processList,
            FileSearchViewModel fileSearch,
            HardwareInfoViewModel hardwareInfo,
            SystemMonitorService systemMonitorService,
            ProcessTelemetryService processTelemetryService,
            ProcessNetworkConnectionService processNetworkConnectionService,
            IUserPreferencesService userPreferencesService,
            IThemeResourceService themeResourceService,
            IProcessSnapshotExportService processSnapshotExportService,
            SettingsViewModel settings)
        {
            _eventBus = eventBus;
            _systemMonitorService = systemMonitorService;
            _processTelemetryService = processTelemetryService;
            _processNetworkConnectionService = processNetworkConnectionService;
            _userPreferencesService = userPreferencesService;
            _themeResourceService = themeResourceService;
            ProcessList = processList;
            FileSearch = fileSearch;
            HardwareInfo = hardwareInfo;
            ProcessManager = new ProcessManagerViewModel(eventBus, processList);
            ToolTree = new ToolTreeViewModel(eventBus);
            ToolContent = new ToolContentViewModel(eventBus, ProcessManager, fileSearch, hardwareInfo, settings);
            OperationLog = new OperationLogPaneViewModel(eventBus);
            StatusBar = new StatusBarViewModel(eventBus, processSnapshotExportService);
            EndProcessConfirmation = new EndProcessConfirmationViewModel(eventBus);
            SelectProcessTool = new DelegateCommand(() => SelectTool(ProcessManagerToolKey));
            SelectFileSearchTool = new DelegateCommand(() => SelectTool(FileSearchToolKey));
            FileSearch.PropertyChanged += HandleFileSearchPropertyChanged;
            HardwareInfo.PropertyChanged += HandleHardwareInfoPropertyChanged;
            _eventBus.Subscribe(this);

            var preferences = _userPreferencesService.Load();
            _selectedThemeKey = ThemeResourceService.ResolvePreferenceThemeKey(preferences.ThemeKey, preferences.IsDarkTheme);
            _rememberWindowSize = preferences.RememberWindowSize;
            InitializeProcessColumnWidths(preferences);
            ApplyApplicationTheme(_selectedThemeKey);
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
            InitializeToolMenu();
            SetLanguage(ResolveStartupCultureName(preferences.CultureName), showStatus: false, persist: false);
            InitializeProcessColumnOptions(preferences);
            StatusMessage = T(NexusDashL.StatusRunning);
            PublishProcessListState();
            PublishShellState();
            PublishSettingsState();
            _refreshLoopTask = RefreshLoopAsync(_refreshCancellation.Token);
        }

        public ProcessListViewModel ProcessList { get; }
        public FileSearchViewModel FileSearch { get; }
        public HardwareInfoViewModel HardwareInfo { get; }
        public ProcessManagerViewModel ProcessManager { get; }
        public ToolTreeViewModel ToolTree { get; }
        public ToolContentViewModel ToolContent { get; }
        public OperationLogPaneViewModel OperationLog { get; }
        public StatusBarViewModel StatusBar { get; }
        public EndProcessConfirmationViewModel EndProcessConfirmation { get; }
        public ObservableCollection<ProcessRowViewModel> VisibleProcesses { get; } = new();
        public ObservableCollection<ProcessColumnOptionViewModel> ProcessColumns { get; } = new();
        public ObservableCollection<LanguageOption> Languages { get; } = new();
        public ObservableCollection<ToolMenuNode> ToolMenuItems { get; } = new();
        public DelegateCommand SelectProcessTool { get; }
        public DelegateCommand SelectFileSearchTool { get; }
        public string ProcessSortColumnKey => _processSortColumnKey;
        public ListSortDirection ProcessSortDirection => _processSortDirection;

        public string WindowTitle => $"{T(NexusDashL.AppName)} - {T(NexusDashL.AppSubtitle)}";
        public string AppNameText => T(NexusDashL.AppName);
        public string AppSubtitleText => T(NexusDashL.AppSubtitle);
        public string SettingsText => T(NexusDashL.Settings);
        public string RememberWindowSizeText => T(NexusDashL.RememberWindowSize);
        public string ProcessManagerText => T(NexusDashL.ProcessManager);
        public string FileSearchToolText => T(NexusDashL.FileSearch);
        public string HardwareInfoToolText => T(NexusDashL.HardwareInfo);
        public string OperationLogText => T(NexusDashL.OperationLog);
        public string OperationLogContent
        {
            get => _operationLogContent;
            private set => SetField(ref _operationLogContent, value, nameof(OperationLogContent));
        }
        public string ProcessOverviewText => T(NexusDashL.ProcessOverview);
        public string ThemeMenuText => T(NexusDashL.ThemeMenu);
        public string DarkThemeText => T(NexusDashL.DarkTheme);
        public string LightThemeText => T(NexusDashL.LightTheme);
        public string LanguageMenuText => T(NexusDashL.LanguageMenu);
        public string PauseText => T(NexusDashL.Pause);
        public string ResumeText => T(NexusDashL.Resume);
        public string SearchPlaceholderText => IsFileSearchToolSelected
            ? FileSearch.SearchPlaceholderText
            : T(NexusDashL.SearchPlaceholder);
        public string SearchNoResultsText => IsSearchActive
            ? string.Format(CultureInfo.CurrentCulture, T(NexusDashL.SearchNoResults), _searchQuery.Trim())
            : T(NexusDashL.FilterNoResults);
        public string EndProcessText => T(NexusDashL.EndProcess);
        public string EndProcessTreeText => T(NexusDashL.EndProcessTree);
        public string EndAssociatedProcessesText => T(NexusDashL.EndAssociatedProcesses);
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
        public string FilterHasNetworkConnectionsText => T(NexusDashL.FilterHasNetworkConnections);
        public string FilterHighCpuText => T(NexusDashL.FilterHighCpu);
        public string FilterUserProcessesText => T(NexusDashL.FilterUserProcesses);
        public string FilterHideSystemProcessesText => T(NexusDashL.FilterHideSystemProcesses);
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
        public string CopyLocalEndpointText => T(NexusDashL.CopyLocalEndpoint);
        public string CopyRemoteEndpointText => T(NexusDashL.CopyRemoteEndpoint);
        public string CopyConnectionInfoText => T(NexusDashL.CopyConnectionInfo);
        public string ExportSnapshotText => T(NexusDashL.ExportSnapshot);
        public string ExportProcessListJsonText => T(NexusDashL.ExportProcessListJson);
        public string ExportProcessListCsvText => T(NexusDashL.ExportProcessListCsv);
        public string ExportSelectedProcessJsonText => T(NexusDashL.ExportSelectedProcessJson);
        public string ExportSelectedProcessCsvText => T(NexusDashL.ExportSelectedProcessCsv);
        public string StatusSnapshotExportedText => T(NexusDashL.StatusSnapshotExported);
        public string StatusSnapshotExportFailedText => T(NexusDashL.StatusSnapshotExportFailed);
        public string StatusSelectedProcessSnapshotExportedText => T(NexusDashL.StatusSelectedProcessSnapshotExported);
        public string ColumnVisibilityText => T(NexusDashL.ColumnVisibility);
        public string RequiredColumnText => T(NexusDashL.RequiredColumn);
        public string ProcessCountText => IsSearchActive
            ? string.Format(CultureInfo.CurrentCulture, T(NexusDashL.SearchResultCount), VisibleProcessCount, ProcessTotalCount)
            : $"{VisibleProcessCount}/{ProcessTotalCount}";
        public string SelectedCountText => string.Format(CultureInfo.CurrentCulture, T(NexusDashL.StatusSelected), SelectedProcessCount);
        public string ActiveStatusMessage => IsFileSearchToolSelected
            ? FileSearch.StatusMessage
            : IsHardwareInfoToolSelected
                ? HardwareInfo.StatusText
                : StatusMessage;
        public string ActiveCountText => IsFileSearchToolSelected
            ? FileSearch.ResultCountText
            : IsHardwareInfoToolSelected
                ? ""
                : IsSettingsToolSelected
                    ? ""
                : SelectedCountText;
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
        public string EndProcessConfirmationTitleText => T(_pendingTerminationKind switch
        {
            ProcessTerminationRequestKind.Associated => NexusDashL.ConfirmEndAssociatedProcessesTitle,
            ProcessTerminationRequestKind.ProcessTree => NexusDashL.ConfirmEndProcessTreeTitle,
            _ => NexusDashL.ConfirmEndProcessTitle
        });
        public string EndProcessConfirmationMessageText => _pendingTerminationKind == ProcessTerminationRequestKind.Associated
            ? string.Format(
                CultureInfo.CurrentCulture,
                T(NexusDashL.ConfirmEndAssociatedProcessesMessage),
                PendingTerminationSelectedCount,
                PendingTerminationTotalCount)
            : string.Format(
                CultureInfo.CurrentCulture,
                T(_pendingTerminationEntireProcessTree
                    ? NexusDashL.ConfirmEndProcessTreeMessage
                    : NexusDashL.ConfirmEndProcessMessage),
                PendingTerminationSelectedCount);
        public string EndProcessConfirmationProcessListText => string.Join(
            Environment.NewLine,
            _pendingTerminationCandidates
                .Where(static candidate => candidate.IsSelected)
                .Take(6)
                .Select(candidate => $"{candidate.Name} ({candidate.Pid})")
                .Concat(PendingTerminationSelectedCount > 6 ? [$"+{PendingTerminationSelectedCount - 6}"] : []));
        public IReadOnlyList<ProcessTerminationCandidateViewModel> PendingTerminationCandidates => _pendingTerminationCandidates;
        public int PendingTerminationSelectedCount => _pendingTerminationCandidates.Count(static candidate => candidate.IsSelected);
        public int PendingTerminationTotalCount => _pendingTerminationCandidates.Count;
        public bool HasSelectedPendingTerminationProcesses => PendingTerminationSelectedCount > 0;
        public bool HasSelectedProcesses => SelectedProcessCount > 0;
        public bool HasSelectedProcess => SelectedProcess is not null;
        public bool IsSearchActive => !string.IsNullOrWhiteSpace(_searchQuery);
        public bool IsProcessToolSelected => string.Equals(_selectedToolKey, ProcessManagerToolKey, StringComparison.Ordinal);
        public bool IsFileSearchToolSelected => string.Equals(_selectedToolKey, FileSearchToolKey, StringComparison.Ordinal);
        public bool IsHardwareInfoToolSelected => string.Equals(_selectedToolKey, HardwareInfoToolKey, StringComparison.Ordinal);
        public bool IsSettingsToolSelected => string.Equals(_selectedToolKey, SettingsToolKey, StringComparison.Ordinal);
        public bool IsSearchBoxVisible => !IsHardwareInfoToolSelected && !IsSettingsToolSelected;
        public bool CanShowPauseRefresh => IsProcessToolSelected && IsRefreshRunning;
        public bool CanShowResumeRefresh => IsProcessToolSelected && IsRefreshPaused;
        public bool IsProcessFilterActive => FilterHasNetworkConnections ||
                                             FilterHighCpu ||
                                             FilterUserProcesses ||
                                             FilterHideSystemProcesses;
        public bool HasNoVisibleProcesses => (IsSearchActive || IsProcessFilterActive) && VisibleProcessCount == 0;
        public bool HasSelectedProcessAccessLimit => SelectedProcess?.IsAccessDenied == true;
        public bool HasSelectedProcessNetworkConnections => SelectedProcessNetworkConnections.Count > 0;
        public bool HasSelectedProcessWithoutNetworkConnections => HasSelectedProcess && !HasSelectedProcessNetworkConnections;
        public bool IsEndProcessConfirmationVisible
        {
            get => _isEndProcessConfirmationVisible;
            private set => this.RaiseAndSetIfChanged(ref _isEndProcessConfirmationVisible, value);
        }
        public bool IsLightTheme => string.Equals(_selectedThemeKey, ThemeResourceService.LightThemeKey, StringComparison.OrdinalIgnoreCase);
        public bool IsRefreshPaused
        {
            get => _isRefreshPaused;
            private set
            {
                if (SetField(ref _isRefreshPaused, value, nameof(IsRefreshPaused)))
                {
                    this.RaisePropertyChanged(nameof(IsRefreshRunning));
                    this.RaisePropertyChanged(nameof(CanShowPauseRefresh));
                    this.RaisePropertyChanged(nameof(CanShowResumeRefresh));
                    PublishStatusBarState();
                }
            }
        }
        public bool IsRefreshRunning => !IsRefreshPaused;
        public bool RememberWindowSize
        {
            get => _rememberWindowSize;
            private set => SetField(ref _rememberWindowSize, value, nameof(RememberWindowSize));
        }

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

        public ToolMenuNode? SelectedToolNode
        {
            get => _selectedToolNode;
            set
            {
                if (value is null)
                {
                    return;
                }

                var toolKey = value.ToolKey;
                if (!TryNormalizeToolKey(toolKey, out var normalizedToolKey))
                {
                    this.RaisePropertyChanged(nameof(SelectedToolNode));
                    return;
                }

                if (SetField(ref _selectedToolNode, value, nameof(SelectedToolNode)))
                {
                    ApplySelectedToolKey(normalizedToolKey);
                }
            }
        }

        public string SearchQuery
        {
            get => IsFileSearchToolSelected ? FileSearch.SearchQuery : _searchQuery;
            set
            {
                if (IsFileSearchToolSelected)
                {
                    FileSearch.SearchQuery = value ?? "";
                    return;
                }

                if (SetField(ref _searchQuery, value ?? "", nameof(SearchQuery)))
                {
                    RebuildVisibleProcesses();
                }
            }
        }

        public bool IsDarkTheme
        {
            get => _themeResourceService.GetThemeOption(_selectedThemeKey).IsDark;
            set
            {
                SetTheme(value ? ThemeResourceService.DarkThemeKey : ThemeResourceService.LightThemeKey);
            }
        }

        public bool FilterHasNetworkConnections
        {
            get => _filterHasNetworkConnections;
            private set => SetFilterField(ref _filterHasNetworkConnections, value, nameof(FilterHasNetworkConnections));
        }

        public bool FilterHighCpu
        {
            get => _filterHighCpu;
            private set => SetFilterField(ref _filterHighCpu, value, nameof(FilterHighCpu));
        }

        public bool FilterUserProcesses
        {
            get => _filterUserProcesses;
            private set => SetFilterField(ref _filterUserProcesses, value, nameof(FilterUserProcesses));
        }

        public bool FilterHideSystemProcesses
        {
            get => _filterHideSystemProcesses;
            private set => SetFilterField(ref _filterHideSystemProcesses, value, nameof(FilterHideSystemProcesses));
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
            set
            {
                if (SetField(ref _statusMessage, value, nameof(StatusMessage)))
                {
                    this.RaisePropertyChanged(nameof(ActiveStatusMessage));
                    PublishStatusBarState();
                    LogOperation(value);
                }
            }
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
                    PublishProcessInspectorState();
                    PublishStatusBarState();
                }
            }
        }

        public IReadOnlyList<TreemapItem> TreemapProcesses
        {
            get => _treemapProcesses;
            set
            {
                if (SetField(ref _treemapProcesses, value, nameof(TreemapProcesses)))
                {
                    PublishProcessExplorerState();
                }
            }
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
                    PublishProcessInspectorState();
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
            SetTheme(ThemeResourceService.DarkThemeKey);
        }

        public void SetLightTheme()
        {
            SetTheme(ThemeResourceService.LightThemeKey);
        }

        private void SetTheme(string themeKey)
        {
            var theme = _themeResourceService.GetThemeOption(themeKey);
            if (string.Equals(_selectedThemeKey, theme.Key, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedThemeKey = theme.Key;
            this.RaisePropertyChanged(nameof(IsDarkTheme));
            this.RaisePropertyChanged(nameof(IsLightTheme));
            ApplyApplicationTheme(theme.Key);
            _userPreferencesService.Update(preferences =>
            {
                preferences.ThemeKey = theme.Key;
                preferences.IsDarkTheme = theme.IsDark;
            });
            PublishProcessListState();
            PublishSettingsState();
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                T(NexusDashL.StatusThemeChanged),
                GetThemeDisplayName(theme.Key));
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

        public void SelectTool(string toolKey)
        {
            if (!TryNormalizeToolKey(toolKey, out var normalizedToolKey))
            {
                normalizedToolKey = ProcessManagerToolKey;
            }

            SelectedToolNode = FindToolNode(normalizedToolKey);
        }

        public void ExecuteActiveSearch()
        {
            if (IsFileSearchToolSelected)
            {
                if (FileSearch.SearchFiles.CanExecute())
                {
                    LogOperation($"执行文件搜索：{FileSearch.SearchQuery}");
                    FileSearch.SearchFiles.Execute();
                }

                return;
            }

            LogOperation($"执行进程筛选：{SearchQuery}");
            RebuildVisibleProcesses();
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

        [EventHandler]
        private void ApplyThemeChange(ThemeChangeRequestedCommand command)
        {
            SetTheme(command.ThemeKey);
        }

        [EventHandler]
        private void ApplyLanguageChange(LanguageChangeRequestedCommand command)
        {
            SetLanguage(command.CultureName);
        }

        [EventHandler]
        private void ApplyToolSelection(ToolSelectionRequestedCommand command)
        {
            SelectTool(command.ToolKey);
        }

        [EventHandler]
        private void ApplyPauseRefresh(PauseRefreshRequestedCommand command)
        {
            PauseRefresh();
        }

        [EventHandler]
        private void ApplyResumeRefresh(ResumeRefreshRequestedCommand command)
        {
            ResumeRefresh();
        }

        [EventHandler]
        private void ApplyCancelPendingProcessTermination(CancelPendingProcessTerminationCommand command)
        {
            CancelPendingProcessTermination();
        }

        [EventHandler]
        private void ApplyConfirmPendingProcessTermination(ConfirmPendingProcessTerminationCommand command)
        {
            ConfirmPendingProcessTermination();
        }

        [EventHandler]
        private void ApplyRememberWindowSizeChange(RememberWindowSizeChangedCommand command)
        {
            if (RememberWindowSize == command.IsEnabled)
            {
                return;
            }

            RememberWindowSize = command.IsEnabled;
            _userPreferencesService.Update(preferences => preferences.RememberWindowSize = command.IsEnabled);
            PublishStatusBarState();
            PublishSettingsState();
            LogOperation($"{RememberWindowSizeText}: {(command.IsEnabled ? "On" : "Off")}");
        }

        [EventHandler]
        private void ApplyStatusMessageRequest(StatusMessageRequestedCommand command)
        {
            StatusMessage = command.Message;
        }

        public void EndSelectedProcesses()
        {
            RequestEndSelectedProcesses(entireProcessTree: false);
        }

        public void EndSelectedProcessTrees()
        {
            RequestEndSelectedProcesses(entireProcessTree: true);
        }

        public void EndSelectedAssociatedProcesses()
        {
            RequestEndSelectedProcesses(entireProcessTree: false, includeAssociatedProcesses: true);
        }

        public void ConfirmPendingProcessTermination()
        {
            var rows = _pendingTerminationCandidates
                .Where(static candidate => candidate.IsSelected)
                .OrderBy(static candidate => candidate.TerminationOrder)
                .ThenByDescending(static candidate => candidate.Process.Depth)
                .ThenBy(static candidate => candidate.Pid)
                .Select(static candidate => candidate.Process)
                .ToArray();
            var entireProcessTree = _pendingTerminationEntireProcessTree;
            IsEndProcessConfirmationVisible = false;
            _pendingTerminationCandidates = [];
            RaiseTerminationConfirmationProperties();
            if (rows.Length > 0)
            {
                _ = EndSelectedProcessesAsync(rows, entireProcessTree);
            }
        }

        public void CancelPendingProcessTermination()
        {
            IsEndProcessConfirmationVisible = false;
            _pendingTerminationCandidates = [];
            RaiseTerminationConfirmationProperties();
        }

        public void SetSelectedProcesses(IEnumerable<ProcessRowViewModel> selectedRows)
        {
            _selectedRows = selectedRows.Where(static row => !row.IsGroupHeader).ToArray();
            SelectedProcess = _selectedRows.LastOrDefault();
            this.RaisePropertyChanged(nameof(SelectedProcessCount));
            this.RaisePropertyChanged(nameof(HasSelectedProcesses));
            this.RaisePropertyChanged(nameof(SelectedCountText));
            this.RaisePropertyChanged(nameof(ActiveCountText));
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

        public void SetProcessColumnWidth(string key, double width)
        {
            if (!ProcessTableSort.TryNormalizeColumnKey(key, out var normalizedKey) ||
                width < 32 ||
                !double.IsFinite(width))
            {
                return;
            }

            width = Math.Round(width, 2);
            if (_processColumnWidths.TryGetValue(normalizedKey, out var savedWidth) &&
                Math.Abs(savedWidth - width) < 0.5)
            {
                return;
            }

            _processColumnWidths[normalizedKey] = width;
            _userPreferencesService.Update(preferences =>
            {
                preferences.ProcessColumnWidths ??= new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                preferences.ProcessColumnWidths[normalizedKey] = width;
            });
            PublishProcessListState();
        }

        public void SetProcessFilter(string key, bool isEnabled)
        {
            switch (key)
            {
                case ProcessFilterHasNetworkConnections:
                    FilterHasNetworkConnections = isEnabled;
                    break;
                case ProcessFilterHighCpu:
                    FilterHighCpu = isEnabled;
                    break;
                case ProcessFilterUserProcesses:
                    FilterUserProcesses = isEnabled;
                    break;
                case ProcessFilterHideSystemProcesses:
                    FilterHideSystemProcesses = isEnabled;
                    break;
                default:
                    return;
            }

            RebuildVisibleProcesses();
        }

        public void SetProcessSort(string columnKey)
        {
            var normalizedColumnKey = ProcessTableSort.NormalizeColumnKey(columnKey);
            var direction = string.Equals(_processSortColumnKey, normalizedColumnKey, StringComparison.OrdinalIgnoreCase)
                ? ProcessTableSort.ToggleDirection(_processSortDirection)
                : ProcessTableSort.GetDefaultDirection(normalizedColumnKey);

            _processSortColumnKey = normalizedColumnKey;
            _processSortDirection = direction;
            this.RaisePropertyChanged(nameof(ProcessSortColumnKey));
            this.RaisePropertyChanged(nameof(ProcessSortDirection));
            RebuildVisibleProcesses();
        }

        [EventHandler]
        private void HandleProcessListSelectionChanged(ProcessListSelectionChangedCommand command)
        {
            SetSelectedProcesses(command.SelectedRows);
        }

        [EventHandler]
        private void HandleProcessTerminationRequested(ProcessTerminationRequestedCommand command)
        {
            RequestEndSelectedProcesses(command.EntireProcessTree, command.IncludeAssociatedProcesses);
        }

        [EventHandler]
        private void HandleProcessColumnVisibilityChanged(ProcessColumnVisibilityChangedCommand command)
        {
            SetProcessColumnVisibility(command.Key, command.IsVisible);
        }

        [EventHandler]
        private void HandleProcessColumnWidthChanged(ProcessColumnWidthChangedCommand command)
        {
            SetProcessColumnWidth(command.Key, command.Width);
        }

        [EventHandler]
        private void HandleProcessFilterChanged(ProcessFilterChangedCommand command)
        {
            SetProcessFilter(command.Key, command.IsEnabled);
        }

        [EventHandler]
        private void HandleProcessSortChanged(ProcessSortChangedCommand command)
        {
            SetProcessSort(command.ColumnKey);
        }

        private void HandleFileSearchPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FileSearchViewModel.SearchQuery) && IsFileSearchToolSelected)
            {
                this.RaisePropertyChanged(nameof(SearchQuery));
            }

            if (e.PropertyName == nameof(FileSearchViewModel.SearchPlaceholderText) && IsFileSearchToolSelected)
            {
                this.RaisePropertyChanged(nameof(SearchPlaceholderText));
            }

            if (e.PropertyName == nameof(FileSearchViewModel.StatusMessage))
            {
                this.RaisePropertyChanged(nameof(ActiveStatusMessage));
                PublishStatusBarState();
            }

            if (e.PropertyName == nameof(FileSearchViewModel.ResultCountText))
            {
                this.RaisePropertyChanged(nameof(ActiveCountText));
                PublishStatusBarState();
            }
        }

        private void HandleHardwareInfoPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HardwareInfoViewModel.StatusText) && IsHardwareInfoToolSelected)
            {
                this.RaisePropertyChanged(nameof(ActiveStatusMessage));
                PublishStatusBarState();
            }
        }

        private void InitializeToolMenu()
        {
            _processManagerNode = new ToolMenuNode(ProcessManagerText, ProcessManagerToolKey, ToolMenuIcon.ProcessManager);
            _fileSearchNode = new ToolMenuNode(FileSearchToolText, FileSearchToolKey, ToolMenuIcon.FileSearch);
            _hardwareInfoNode = new ToolMenuNode(HardwareInfoToolText, HardwareInfoToolKey, ToolMenuIcon.HardwareInfo);
            _settingsNode = new ToolMenuNode(SettingsText, SettingsToolKey, ToolMenuIcon.Settings);
            ToolMenuItems.Clear();
            ToolMenuItems.Add(_processManagerNode);
            ToolMenuItems.Add(_fileSearchNode);
            ToolMenuItems.Add(_hardwareInfoNode);
            ToolMenuItems.Add(_settingsNode);
            _selectedToolNode = _processManagerNode;
        }

        private ToolMenuNode FindToolNode(string toolKey)
        {
            return toolKey switch
            {
                FileSearchToolKey => _fileSearchNode ?? _selectedToolNode ?? ToolMenuItems.First(),
                HardwareInfoToolKey => _hardwareInfoNode ?? _selectedToolNode ?? ToolMenuItems.First(),
                SettingsToolKey => _settingsNode ?? _selectedToolNode ?? ToolMenuItems.First(),
                _ => _processManagerNode ?? _selectedToolNode ?? ToolMenuItems.First()
            };
        }

        private void ApplySelectedToolKey(string toolKey)
        {
            if (SetField(ref _selectedToolKey, toolKey, nameof(_selectedToolKey)))
            {
                RaiseActiveToolProperties();
                PublishToolTreeState();
                PublishActiveToolState();
                PublishStatusBarState();
                LogOperation($"切换工具：{GetToolDisplayName(toolKey)}");
                if (IsProcessToolSelected)
                {
                    ApplyDeferredProcessLocalizationRefresh();
                    QueueDeferredProcessTreeUpdate();
                }
            }
        }

        private static bool TryNormalizeToolKey(string? toolKey, out string normalizedToolKey)
        {
            if (string.Equals(toolKey, FileSearchToolKey, StringComparison.Ordinal))
            {
                normalizedToolKey = FileSearchToolKey;
                return true;
            }

            if (string.Equals(toolKey, HardwareInfoToolKey, StringComparison.Ordinal))
            {
                normalizedToolKey = HardwareInfoToolKey;
                return true;
            }

            if (string.Equals(toolKey, SettingsToolKey, StringComparison.Ordinal))
            {
                normalizedToolKey = SettingsToolKey;
                return true;
            }

            if (string.Equals(toolKey, ProcessManagerToolKey, StringComparison.Ordinal))
            {
                normalizedToolKey = ProcessManagerToolKey;
                return true;
            }

            normalizedToolKey = ProcessManagerToolKey;
            return false;
        }

        private void RefreshToolMenuHeaders()
        {
            if (_processManagerNode is not null)
            {
                _processManagerNode.Header = ProcessManagerText;
            }

            if (_fileSearchNode is not null)
            {
                _fileSearchNode.Header = FileSearchToolText;
            }

            if (_hardwareInfoNode is not null)
            {
                _hardwareInfoNode.Header = HardwareInfoToolText;
            }

            if (_settingsNode is not null)
            {
                _settingsNode.Header = SettingsText;
            }

            PublishToolTreeState();
            PublishProcessManagerState();
        }

        private void RaiseActiveToolProperties()
        {
            this.RaisePropertyChanged(nameof(IsProcessToolSelected));
            this.RaisePropertyChanged(nameof(IsFileSearchToolSelected));
            this.RaisePropertyChanged(nameof(IsHardwareInfoToolSelected));
            this.RaisePropertyChanged(nameof(IsSettingsToolSelected));
            this.RaisePropertyChanged(nameof(IsSearchBoxVisible));
            this.RaisePropertyChanged(nameof(CanShowPauseRefresh));
            this.RaisePropertyChanged(nameof(CanShowResumeRefresh));
            this.RaisePropertyChanged(nameof(SearchPlaceholderText));
            this.RaisePropertyChanged(nameof(SearchQuery));
            this.RaisePropertyChanged(nameof(ActiveStatusMessage));
            this.RaisePropertyChanged(nameof(ActiveCountText));
        }

        private string GetToolDisplayName(string toolKey)
        {
            return toolKey switch
            {
                FileSearchToolKey => FileSearchToolText,
                HardwareInfoToolKey => HardwareInfoToolText,
                SettingsToolKey => SettingsText,
                _ => ProcessManagerText
            };
        }

        private void LogOperation(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Logger.Info(message, message, log2Console: false);
                AppendOperationLogMessage(message);
            }
        }

        private void AppendOperationLogMessage(string message)
        {
            const int maxLogLines = 120;

            var line = $"{DateTime.Now:HH:mm:ss} [消息] {message}";
            var nextLines = string.IsNullOrWhiteSpace(OperationLogContent)
                ? [line]
                : OperationLogContent
                    .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                    .Append(line)
                    .TakeLast(maxLogLines)
                    .ToArray();

            OperationLogContent = string.Join(Environment.NewLine, nextLines);
            PublishOperationLogState();
        }

        private void PublishProcessListState()
        {
            _eventBus.Publish(new ProcessListStateChangedCommand(new ProcessListState
            {
                VisibleProcesses = VisibleProcesses.ToArray(),
                ProcessColumns = ProcessColumns.ToArray(),
                ProcessColumnWidths = new Dictionary<string, double>(_processColumnWidths, StringComparer.OrdinalIgnoreCase),
                SelectedProcessPid = SelectedProcess?.Pid,
                ProcessSortColumnKey = ProcessSortColumnKey,
                ProcessSortDirection = ProcessSortDirection,
                FilterHasNetworkConnections = FilterHasNetworkConnections,
                FilterHighCpu = FilterHighCpu,
                FilterUserProcesses = FilterUserProcesses,
                FilterHideSystemProcesses = FilterHideSystemProcesses,
                ProcessTreeText = ProcessTreeText,
                ProcessCountText = ProcessCountText,
                SearchNoResultsText = SearchNoResultsText,
                EndProcessText = EndProcessText,
                EndProcessTreeText = EndProcessTreeText,
                EndAssociatedProcessesText = EndAssociatedProcessesText,
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
                FilterHasNetworkConnectionsText = FilterHasNetworkConnectionsText,
                FilterHighCpuText = FilterHighCpuText,
                FilterUserProcessesText = FilterUserProcessesText,
                FilterHideSystemProcessesText = FilterHideSystemProcessesText,
                ColumnVisibilityText = ColumnVisibilityText,
                RequiredColumnText = RequiredColumnText,
                HasSelectedProcesses = HasSelectedProcesses,
                HasNoVisibleProcesses = HasNoVisibleProcesses
            }));
            PublishStatusBarState();
        }

        private void PublishShellState()
        {
            PublishToolTreeState();
            PublishActiveToolState();
            PublishProcessManagerState();
            PublishProcessOverviewState();
            PublishProcessExplorerState();
            PublishProcessInspectorState();
            PublishStatusBarState();
            PublishEndProcessConfirmationState();
            PublishOperationLogState();
        }

        private void PublishToolTreeState()
        {
            _eventBus.Publish(new ToolTreeStateChangedCommand(new ToolTreeState
            {
                ToolMenuItems = ToolMenuItems.ToArray(),
                SelectedToolNode = SelectedToolNode
            }));
        }

        private void PublishActiveToolState()
        {
            _eventBus.Publish(new ActiveToolStateChangedCommand(new ActiveToolState
            {
                IsProcessToolSelected = IsProcessToolSelected,
                IsFileSearchToolSelected = IsFileSearchToolSelected,
                IsHardwareInfoToolSelected = IsHardwareInfoToolSelected,
                IsSettingsToolSelected = IsSettingsToolSelected
            }));
        }

        private void PublishProcessManagerState()
        {
            _eventBus.Publish(new ProcessManagerStateChangedCommand(new ProcessManagerState
            {
                ProcessOverviewText = ProcessOverviewText,
                ProcessTreeText = ProcessTreeText,
                DetailsText = DetailsText
            }));
        }

        private void PublishProcessOverviewState()
        {
            _eventBus.Publish(new ProcessOverviewStateChangedCommand(new ProcessOverviewState
            {
                CpuText = CpuText,
                MemoryText = MemoryText,
                DiskText = DiskText,
                NetworkText = NetworkText,
                CpuUsageText = CpuUsageText,
                MemoryUsageText = MemoryUsageText,
                MemorySummaryText = MemorySummaryText,
                DiskSpeedText = DiskSpeedText,
                NetworkSpeedText = NetworkSpeedText,
                TopCpuProcessText = TopCpuProcessText,
                TopMemoryProcessText = TopMemoryProcessText,
                TopDiskProcessText = TopDiskProcessText,
                TopNetworkProcessText = TopNetworkProcessText,
                CpuUsage = CpuUsage,
                CpuHistory = CpuHistory,
                MemoryHistory = MemoryHistory,
                DiskHistory = DiskHistory,
                NetworkHistory = NetworkHistory
            }));
        }

        private void PublishProcessExplorerState()
        {
            _eventBus.Publish(new ProcessExplorerStateChangedCommand(new ProcessExplorerState
            {
                TreemapText = TreemapText,
                TreemapProcesses = TreemapProcesses
            }));
        }

        private void PublishProcessInspectorState()
        {
            _eventBus.Publish(new ProcessInspectorStateChangedCommand(new ProcessInspectorState
            {
                DetailsText = DetailsText,
                HandlesText = HandlesText,
                NetworkText = NetworkText,
                ServicesText = ServicesText,
                StartupText = StartupText,
                NoProcessSelectedText = NoProcessSelectedText,
                AccessLimitedText = AccessLimitedText,
                AccessLimitedDescriptionText = AccessLimitedDescriptionText,
                PidText = PidText,
                PublisherText = PublisherText,
                StartTimeText = StartTimeText,
                CpuText = CpuText,
                MemoryText = MemoryText,
                PathText = PathText,
                CommandLineText = CommandLineText,
                HandlesSearchPlaceholderText = HandlesSearchPlaceholderText,
                HandlesUnavailableText = HandlesUnavailableText,
                ServicesUnavailableText = ServicesUnavailableText,
                StartupUnavailableText = StartupUnavailableText,
                ProcessNetworkConnectionsText = ProcessNetworkConnectionsText,
                SelectedProcessNetworkSummaryText = SelectedProcessNetworkSummaryText,
                SelectedProcessConnectionTotalText = SelectedProcessConnectionTotalText,
                SelectedProcessTcpConnectionCountText = SelectedProcessTcpConnectionCountText,
                SelectedProcessUdpConnectionCountText = SelectedProcessUdpConnectionCountText,
                NetworkSelectProcessText = NetworkSelectProcessText,
                NetworkNoConnectionsText = NetworkNoConnectionsText,
                ProtocolText = ProtocolText,
                LocalEndpointText = LocalEndpointText,
                RemoteEndpointText = RemoteEndpointText,
                StateText = StateText,
                LastSeenText = LastSeenText,
                CopyLocalEndpointText = CopyLocalEndpointText,
                CopyRemoteEndpointText = CopyRemoteEndpointText,
                CopyConnectionInfoText = CopyConnectionInfoText,
                SelectedProcess = SelectedProcess,
                HasSelectedProcess = HasSelectedProcess,
                HasSelectedProcessAccessLimit = HasSelectedProcessAccessLimit,
                HasSelectedProcessNetworkConnections = HasSelectedProcessNetworkConnections,
                HasSelectedProcessWithoutNetworkConnections = HasSelectedProcessWithoutNetworkConnections,
                SelectedProcessNetworkConnections = SelectedProcessNetworkConnections
            }));
        }

        private void PublishStatusBarState()
        {
            _eventBus.Publish(new StatusBarStateChangedCommand(new StatusBarState
            {
                PauseText = PauseText,
                ResumeText = ResumeText,
                ExportSnapshotText = ExportSnapshotText,
                ExportProcessListJsonText = ExportProcessListJsonText,
                ExportProcessListCsvText = ExportProcessListCsvText,
                ExportSelectedProcessJsonText = ExportSelectedProcessJsonText,
                ExportSelectedProcessCsvText = ExportSelectedProcessCsvText,
                StatusSnapshotExportedText = StatusSnapshotExportedText,
                StatusSnapshotExportFailedText = StatusSnapshotExportFailedText,
                StatusSelectedProcessSnapshotExportedText = StatusSelectedProcessSnapshotExportedText,
                ActiveStatusMessage = ActiveStatusMessage,
                ActiveCountText = ActiveCountText,
                CanShowPauseRefresh = CanShowPauseRefresh,
                CanShowResumeRefresh = CanShowResumeRefresh,
                IsProcessToolSelected = IsProcessToolSelected,
                HasSelectedProcess = HasSelectedProcess,
                ProcessTotalCount = ProcessTotalCount,
                SelectedProcess = SelectedProcess,
                VisibleProcesses = _visibleProcessRowsForExport
            }));
        }

        private void PublishEndProcessConfirmationState()
        {
            _eventBus.Publish(new EndProcessConfirmationStateChangedCommand(new EndProcessConfirmationState
            {
                IsEndProcessConfirmationVisible = IsEndProcessConfirmationVisible,
                EndProcessConfirmationTitleText = EndProcessConfirmationTitleText,
                EndProcessConfirmationMessageText = EndProcessConfirmationMessageText,
                PendingTerminationCandidates = PendingTerminationCandidates,
                HasSelectedPendingTerminationProcesses = HasSelectedPendingTerminationProcesses,
                CancelText = CancelText,
                ConfirmText = ConfirmText
            }));
        }

        private void PublishOperationLogState()
        {
            _eventBus.Publish(new OperationLogStateChangedCommand(new OperationLogState
            {
                OperationLogText = OperationLogText,
                OperationLogContent = OperationLogContent
            }));
        }

        private void PublishSettingsState()
        {
            _eventBus.Publish(new SettingsStateChangedCommand(
                _selectedThemeKey,
                IsDarkTheme,
                RememberWindowSize,
                _selectedCultureName));
        }

        private void RequestEndSelectedProcesses(bool entireProcessTree, bool includeAssociatedProcesses = false)
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

            var candidates = includeAssociatedProcesses
                ? CreateAssociatedProcessTerminationCandidates(rows)
                : CreateProcessTerminationCandidates(rows, relationText: T(NexusDashL.TerminationRelationSelected));
            if (candidates.Length == 0)
            {
                return;
            }

            _pendingTerminationCandidates = candidates;
            _pendingTerminationKind = includeAssociatedProcesses
                ? ProcessTerminationRequestKind.Associated
                : entireProcessTree
                    ? ProcessTerminationRequestKind.ProcessTree
                    : ProcessTerminationRequestKind.Process;
            _pendingTerminationEntireProcessTree = entireProcessTree && !includeAssociatedProcesses;
            IsEndProcessConfirmationVisible = true;
            RaiseTerminationConfirmationProperties();
            Logger.Warn(
                $"Process termination confirmation requested: kind={_pendingTerminationKind}; candidates={candidates.Length}; pids={string.Join(", ", candidates.Select(static candidate => candidate.Pid))}",
                $"请求结束进程确认：{candidates.Length} 个候选进程",
                log2Console: false);
        }

        private ProcessTerminationCandidateViewModel[] CreateProcessTerminationCandidates(
            IReadOnlyList<ProcessRowViewModel> rows,
            string relationText)
        {
            return rows
                .OrderBy(static row => row.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(static row => row.Pid)
                .Select((row, index) => CreateTerminationCandidate(
                    row,
                    relationText,
                    displayOrder: index,
                    terminationOrder: 1000 + index))
                .ToArray();
        }

        private ProcessTerminationCandidateViewModel[] CreateAssociatedProcessTerminationCandidates(
            IReadOnlyList<ProcessRowViewModel> selectedRows)
        {
            var candidates = new Dictionary<int, ProcessTerminationCandidateInfo>();

            foreach (var row in selectedRows)
            {
                AddTerminationCandidate(
                    candidates,
                    row,
                    T(NexusDashL.TerminationRelationSelected),
                    relationPriority: 0,
                    displayOrder: 1000,
                    terminationOrder: 1000 - row.Depth);

                if (row.ParentPid is { } parentPid &&
                    parentPid != row.Pid &&
                    _rowCache.TryGetValue(parentPid, out var parent) &&
                    !parent.IsGroupHeader &&
                    IsPlausibleParentProcess(parent, row))
                {
                    AddTerminationCandidate(
                        candidates,
                        parent,
                        T(NexusDashL.TerminationRelationParent),
                        relationPriority: 1,
                        displayOrder: 0,
                        terminationOrder: 2000 - parent.Depth);
                }

                foreach (var child in GetProcessDescendants(row))
                {
                    AddTerminationCandidate(
                        candidates,
                        child,
                        T(NexusDashL.TerminationRelationChild),
                        relationPriority: 2,
                        displayOrder: 2000,
                        terminationOrder: 0 - child.Depth);
                }
            }

            return candidates.Values
                .OrderBy(static candidate => candidate.DisplayOrder)
                .ThenBy(static candidate => candidate.Row.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(static candidate => candidate.Row.Pid)
                .Select(candidate => CreateTerminationCandidate(
                    candidate.Row,
                    candidate.RelationText,
                    candidate.DisplayOrder,
                    candidate.TerminationOrder))
                .ToArray();
        }

        private static void AddTerminationCandidate(
            IDictionary<int, ProcessTerminationCandidateInfo> candidates,
            ProcessRowViewModel row,
            string relationText,
            int relationPriority,
            int displayOrder,
            int terminationOrder)
        {
            if (row.IsGroupHeader)
            {
                return;
            }

            if (!candidates.TryGetValue(row.Pid, out var existing) ||
                relationPriority < existing.RelationPriority)
            {
                candidates[row.Pid] = new ProcessTerminationCandidateInfo(
                    row,
                    relationText,
                    relationPriority,
                    displayOrder,
                    terminationOrder);
            }
        }

        private ProcessTerminationCandidateViewModel CreateTerminationCandidate(
            ProcessRowViewModel row,
            string relationText,
            int displayOrder,
            int terminationOrder)
        {
            return new ProcessTerminationCandidateViewModel(
                row,
                relationText,
                T(NexusDashL.MetricUnavailable),
                displayOrder,
                terminationOrder,
                HandlePendingTerminationCandidateSelectionChanged);
        }

        private IEnumerable<ProcessRowViewModel> GetProcessDescendants(ProcessRowViewModel row)
        {
            var visitedPids = new HashSet<int> { row.Pid };
            return GetProcessDescendants(row, visitedPids);
        }

        private IEnumerable<ProcessRowViewModel> GetProcessDescendants(
            ProcessRowViewModel row,
            ISet<int> visitedPids)
        {
            foreach (var child in _rowCache.Values
                         .Where(candidate => candidate.ParentPid == row.Pid && candidate.Pid != row.Pid)
                         .OrderBy(static candidate => candidate.Name, StringComparer.CurrentCultureIgnoreCase)
                         .ThenBy(static candidate => candidate.Pid))
            {
                if (!IsPlausibleParentProcess(row, child))
                {
                    continue;
                }

                if (!visitedPids.Add(child.Pid))
                {
                    continue;
                }

                yield return child;

                foreach (var descendant in GetProcessDescendants(child, visitedPids))
                {
                    yield return descendant;
                }
            }
        }

        private static bool IsPlausibleParentProcess(ProcessRowViewModel parent, ProcessRowViewModel child)
        {
            return parent.StartTime is not { } parentStart ||
                   child.StartTime is not { } childStart ||
                   parentStart <= childStart;
        }

        private void HandlePendingTerminationCandidateSelectionChanged(ProcessTerminationCandidateViewModel candidate)
        {
            RaiseTerminationConfirmationProperties();
        }

        private void RefreshSelectedNetworkConnections()
        {
            IReadOnlyList<ProcessNetworkConnection> nextConnections;
            if (SelectedProcess is null)
            {
                nextConnections = [];
            }
            else
            {
                nextConnections = _networkConnections
                    .Where(connection => connection.Pid == SelectedProcess.Pid)
                    .OrderBy(static connection => connection.Protocol, StringComparer.Ordinal)
                    .ThenBy(static connection => connection.State, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(static connection => connection.LocalPort)
                    .ThenBy(static connection => connection.RemoteEndpointText, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
            }

            if (!AreSameNetworkConnections(_selectedProcessNetworkConnections, nextConnections))
            {
                SelectedProcessNetworkConnections = nextConnections;
            }
        }

        private static bool AreSameNetworkConnections(
            IReadOnlyList<ProcessNetworkConnection> left,
            IReadOnlyList<ProcessNetworkConnection> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Count; index++)
            {
                var leftConnection = left[index];
                var rightConnection = right[index];
                if (!string.Equals(leftConnection.Protocol, rightConnection.Protocol, StringComparison.Ordinal) ||
                    leftConnection.Pid != rightConnection.Pid ||
                    !string.Equals(leftConnection.ProcessName, rightConnection.ProcessName, StringComparison.Ordinal) ||
                    !string.Equals(leftConnection.LocalAddress, rightConnection.LocalAddress, StringComparison.Ordinal) ||
                    leftConnection.LocalPort != rightConnection.LocalPort ||
                    !string.Equals(leftConnection.RemoteAddress, rightConnection.RemoteAddress, StringComparison.Ordinal) ||
                    leftConnection.RemotePort != rightConnection.RemotePort ||
                    !string.Equals(leftConnection.State, rightConnection.State, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private async Task RefreshLoopAsync(CancellationToken cancellationToken)
        {
            while (!_isDisposed && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!IsRefreshPaused)
                    {
                        await RefreshAsync(cancellationToken).ConfigureAwait(false);
                    }

                    await Task.Delay(_refreshInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    if (_isDisposed || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (!_isDisposed)
                        {
                            StatusMessage = exception.Message;
                        }
                    });
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task RefreshAsync(CancellationToken cancellationToken)
        {
            var systemTask = _systemMonitorService.GetMetricsAsync();
            var refreshProcesses = ShouldRefreshProcessSnapshot(DateTime.UtcNow);
            var processTask = refreshProcesses
                ? _processTelemetryService.GetProcessesAsync()
                : null;
            var networkConnections = GetLatestNetworkConnectionsSnapshot();
            var systemMetrics = await systemTask.ConfigureAwait(false);
            var processes = _latestProcessSnapshot;
            if (processTask is not null)
            {
                processes = await processTask.ConfigureAwait(false);
                _lastProcessSnapshotRefreshUtc = DateTime.UtcNow;
            }

            var snapshot = PrepareRefreshSnapshot(
                systemMetrics,
                processes,
                networkConnections,
                processTask is not null);

            if (_isDisposed || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_isDisposed && !cancellationToken.IsCancellationRequested)
                {
                    ApplySnapshot(snapshot);
                }
            });
        }

        private bool ShouldRefreshProcessSnapshot(DateTime now)
        {
            return _latestProcessSnapshot.Count == 0 ||
                   (IsProcessToolSelected &&
                    now - _lastProcessSnapshotRefreshUtc >= _processSnapshotRefreshInterval);
        }

        private static RefreshSnapshot PrepareRefreshSnapshot(
            SystemMetrics systemMetrics,
            IReadOnlyList<ProcessMetrics> processes,
            IReadOnlyList<ProcessNetworkConnection> networkConnections,
            bool processesUpdated)
        {
            var processSnapshot = processes.ToArray();
            var enrichedNetworkConnections = EnrichNetworkConnections(networkConnections, processSnapshot);
            ApplyProcessNetworkCounts(processSnapshot, enrichedNetworkConnections);
            var processCpuUsage = Math.Min(100, processSnapshot.Sum(static process => process.CpuPercent));
            var cpuUsage = systemMetrics.Cpu.TotalUsage > 0 || !processesUpdated
                ? systemMetrics.Cpu.TotalUsage
                : processCpuUsage;
            var diskBytesPerSecond = processSnapshot.Sum(static process =>
                process.DiskReadBytesPerSecond + process.DiskWriteBytesPerSecond);
            var networkBytesPerSecond = systemMetrics.Network.UploadSpeed + systemMetrics.Network.DownloadSpeed;

            return new RefreshSnapshot(
                systemMetrics,
                processSnapshot,
                enrichedNetworkConnections,
                cpuUsage,
                diskBytesPerSecond,
                networkBytesPerSecond,
                processesUpdated);
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

            var now = DateTime.UtcNow;
            if (_networkRefreshTask is null &&
                now - _lastNetworkConnectionRefreshUtc >= _networkConnectionRefreshInterval)
            {
                _lastNetworkConnectionRefreshUtc = now;
                _networkRefreshTask = _processNetworkConnectionService.GetConnectionsAsync();
            }

            return _networkConnections;
        }

        private void ApplySnapshot(RefreshSnapshot snapshot)
        {
            var processSnapshot = snapshot.Processes;
            CpuUsage = snapshot.CpuUsage;
            MemoryUsage = snapshot.SystemMetrics.Memory.UsagePercentage;
            DiskBytesPerSecond = snapshot.DiskBytesPerSecond;
            NetworkBytesPerSecond = snapshot.NetworkBytesPerSecond;
            MemoryUsedText = ProcessRowViewModel.FormatBytes(snapshot.SystemMetrics.Memory.UsedBytes);
            MemoryTotalText = ProcessRowViewModel.FormatBytes(snapshot.SystemMetrics.Memory.TotalBytes);

            CpuHistory = AppendHistory(CpuHistory, snapshot.CpuUsage);
            MemoryHistory = AppendHistory(MemoryHistory, MemoryUsage);
            DiskHistory = AppendHistory(DiskHistory, Math.Min(100, snapshot.DiskBytesPerSecond / 1024 / 1024));
            NetworkHistory = AppendHistory(NetworkHistory, Math.Min(100, snapshot.NetworkBytesPerSecond / 1024 / 1024));

            _networkConnections = snapshot.NetworkConnections;
            RefreshSelectedNetworkConnections();

            if (snapshot.ProcessesUpdated)
            {
                UpdateTopProcessInsights(processSnapshot);
                ProcessTotalCount = processSnapshot.Count;
                _latestProcessSnapshot = processSnapshot;
            }

            if (IsProcessToolSelected)
            {
                if (snapshot.ProcessesUpdated && ShouldRefreshProcessTree())
                {
                    RebuildProcessTree(processSnapshot);
                    _lastProcessTreeRefreshUtc = DateTime.UtcNow;
                    _hasDeferredProcessTreeUpdate = false;
                }
                else if (snapshot.ProcessesUpdated)
                {
                    _hasDeferredProcessTreeUpdate = true;
                }

                PublishProcessOverviewState();
            }
            else if (snapshot.ProcessesUpdated)
            {
                _hasDeferredProcessTreeUpdate = true;
            }

            if (snapshot.ProcessesUpdated)
            {
                PublishStatusBarState();
            }
        }

        private bool ShouldRefreshProcessTree()
        {
            return !_hasBuiltProcessTree ||
                   DateTime.UtcNow - _lastProcessTreeRefreshUtc >= _processTreeRefreshInterval;
        }

        private void QueueDeferredProcessTreeUpdate()
        {
            if (!_hasDeferredProcessTreeUpdate || _latestProcessSnapshot.Count == 0)
            {
                return;
            }

            Dispatcher.UIThread.Post(ApplyDeferredProcessTreeUpdate, DispatcherPriority.Background);
        }

        private void ApplyDeferredProcessTreeUpdate()
        {
            if (_isDisposed ||
                !IsProcessToolSelected ||
                !_hasDeferredProcessTreeUpdate ||
                _latestProcessSnapshot.Count == 0)
            {
                return;
            }

            RebuildProcessTree(_latestProcessSnapshot);
            _lastProcessTreeRefreshUtc = DateTime.UtcNow;
            _hasDeferredProcessTreeUpdate = false;
            PublishProcessOverviewState();
            PublishProcessExplorerState();
            PublishProcessInspectorState();
            PublishStatusBarState();
        }

        private void QueueDeferredProcessLocalizationRefresh()
        {
            _hasDeferredProcessLocalizationRefresh = true;
            if (!IsProcessToolSelected)
            {
                return;
            }

            Dispatcher.UIThread.Post(ApplyDeferredProcessLocalizationRefresh, DispatcherPriority.Background);
        }

        private void ApplyDeferredProcessLocalizationRefresh()
        {
            if (_isDisposed || !_hasDeferredProcessLocalizationRefresh)
            {
                return;
            }

            var unavailableText = T(NexusDashL.MetricUnavailable);
            foreach (var row in _rowCache.Values)
            {
                row.RefreshLocalizedText(unavailableText);
            }

            _applicationGroupRow.RefreshLocalizedText(unavailableText);
            _backgroundGroupRow.RefreshLocalizedText(unavailableText);
            _windowsGroupRow.RefreshLocalizedText(unavailableText);
            _hasDeferredProcessLocalizationRefresh = false;

            if (IsProcessToolSelected)
            {
                RebuildVisibleProcesses();
            }
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
                else
                {
                    process.TcpConnectionCount = 0;
                    process.UdpConnectionCount = 0;
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
            var treeStructureChanged = !_hasBuiltProcessTree;
            var staticTextChanged = false;
            var liveMetricsChanged = false;
            var activePids = processes.Select(static p => p.Pid).ToHashSet();
            foreach (var stalePid in _rowCache.Keys.Where(pid => !activePids.Contains(pid)).ToArray())
            {
                _rowCache.Remove(stalePid);
                _expandedPids.Remove(stalePid);
                _collapsedPids.Remove(stalePid);
                treeStructureChanged = true;
            }

            var unavailableText = T(NexusDashL.MetricUnavailable);
            foreach (var process in processes)
            {
                if (_rowCache.TryGetValue(process.Pid, out var row))
                {
                    var flags = row.Update(process);
                    treeStructureChanged |= (flags & ProcessRowUpdateFlags.Structure) != 0;
                    staticTextChanged |= (flags & ProcessRowUpdateFlags.StaticText) != 0;
                    liveMetricsChanged |= (flags & ProcessRowUpdateFlags.LiveMetrics) != 0;
                }
                else
                {
                    row = new ProcessRowViewModel(process, unavailableText, HandleRowExpansionChanged);
                    _rowCache[process.Pid] = row;
                    treeStructureChanged = true;
                    staticTextChanged = true;
                    liveMetricsChanged = true;
                }
            }

            if (treeStructureChanged)
            {
                RebuildProcessHierarchy();
                _hasBuiltProcessTree = true;
            }
            else if (staticTextChanged && IsStaticSortColumn(_processSortColumnKey))
            {
                SortAndAssignDepth(_rootRows, 1);
            }

            if (ShouldRebuildVisibleProcesses(treeStructureChanged, staticTextChanged, liveMetricsChanged))
            {
                RebuildVisibleProcesses();
            }

            UpdateTreemapProcesses(treeStructureChanged);

            if (SelectedProcess is not null && _rowCache.TryGetValue(SelectedProcess.Pid, out var refreshedSelection))
            {
                SelectedProcess = refreshedSelection;
            }
        }

        private void UpdateTreemapProcesses(bool force)
        {
            var now = DateTime.UtcNow;
            if (!force && now - _lastTreemapRefreshUtc < _treemapRefreshInterval)
            {
                return;
            }

            _lastTreemapRefreshUtc = now;
            TreemapProcesses = _rowCache.Values
                .OrderByDescending(static row => row.WorkingSetBytes)
                .Take(32)
                .Select(static row => new TreemapItem(row.Name, row.MemoryText, row.WorkingSetBytes))
                .ToArray();
        }

        private void RebuildProcessHierarchy()
        {
            _rootRows.Clear();
            foreach (var row in _rowCache.Values)
            {
                row.Parent = null;
                row.Children.Clear();
            }

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
                    _rootRows.Add(row);
                }
            }

            SortAndAssignDepth(_rootRows, 1);
        }

        private bool ShouldRebuildVisibleProcesses(
            bool treeStructureChanged,
            bool staticTextChanged,
            bool liveMetricsChanged)
        {
            if (treeStructureChanged ||
                IsProcessFilterActive ||
                (IsSearchActive && staticTextChanged))
            {
                return true;
            }

            if (IsLiveSortColumn(_processSortColumnKey))
            {
                return liveMetricsChanged || staticTextChanged;
            }

            return staticTextChanged && IsStaticSortColumn(_processSortColumnKey);
        }

        private static bool IsLiveSortColumn(string columnKey)
        {
            return columnKey is ProcessTableColumns.Cpu or
                   ProcessTableColumns.Memory or
                   ProcessTableColumns.Disk or
                   ProcessTableColumns.Network or
                   ProcessTableColumns.Gpu;
        }

        private static bool IsStaticSortColumn(string columnKey)
        {
            return columnKey is ProcessTableColumns.Pid or
                   ProcessTableColumns.ParentPid or
                   ProcessTableColumns.Name or
                   ProcessTableColumns.Publisher;
        }

        private void SortAndAssignDepth(IList<ProcessRowViewModel> rows, int depth)
        {
            var sorted = SortProcessRows(rows).ToArray();

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
            var showFlatMatches = query.Length > 0 || IsProcessFilterActive;

            foreach (var category in GetProcessCategoryOrder())
            {
                var groupRow = GetGroupRow(category);
                groupRow.Children.Clear();

                if (!showFlatMatches)
                {
                    var roots = GetCategoryRoots(category)
                        .ToArray();

                    if (roots.Length == 0)
                    {
                        continue;
                    }

                    foreach (var root in roots)
                    {
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
                        CollectFilteredRows(root, category, query, matches);
                    }

                    if (matches.Count == 0)
                    {
                        continue;
                    }

                    foreach (var match in SortProcessRows(matches))
                    {
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
            _visibleProcessRowsForExport = visible.Where(static row => row.IsProcessRow).ToArray();
            RaiseProcessVisibilityProperties();
            PublishProcessListState();
        }

        private void RaiseProcessVisibilityProperties()
        {
            this.RaisePropertyChanged(nameof(IsSearchActive));
            this.RaisePropertyChanged(nameof(IsProcessFilterActive));
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

        private void CollectFilteredRows(
            ProcessRowViewModel row,
            ProcessCategory category,
            string query,
            IList<ProcessRowViewModel> matches)
        {
            if (row.Category == category &&
                row.Matches(query) &&
                PassesProcessFilters(row))
            {
                matches.Add(row);
            }

            foreach (var child in row.Children)
            {
                CollectFilteredRows(child, category, query, matches);
            }
        }

        private bool PassesProcessFilters(ProcessRowViewModel row)
        {
            if (row.IsGroupHeader)
            {
                return false;
            }

            return (!FilterHasNetworkConnections || row.NetworkConnectionCount > 0) &&
                   (!FilterHighCpu || row.CpuPercent >= 1.0) &&
                   (!FilterUserProcesses || row.Category == ProcessCategory.Application) &&
                   (!FilterHideSystemProcesses || row.Category != ProcessCategory.WindowsProcess);
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
            return SortProcessRows(_rowCache.Values
                .Where(row => !row.IsGroupHeader &&
                              row.Category == category &&
                              (row.Parent is null || row.Parent.Category != category)));
        }

        private IEnumerable<ProcessRowViewModel> SortProcessRows(IEnumerable<ProcessRowViewModel> rows)
        {
            return rows.OrderBy(row => row, new ProcessTableSort.RowComparer(_processSortColumnKey, _processSortDirection));
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
                    target[index] = source[index];
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
                Logger.Warn(
                    $"Ending processes: entireProcessTree={entireProcessTree}; pids={string.Join(", ", pids)}",
                    $"开始结束进程：{string.Join(", ", pids)}",
                    log2Console: false);
                await Task.Run(() =>
                {
                    foreach (var pid in pids)
                    {
                        _processTelemetryService.EndProcess(pid, entireProcessTree);
                    }
                });

                StatusMessage = string.Format(CultureInfo.CurrentCulture, T(NexusDashL.StatusEnded), pids.Length);
                Logger.Info(
                    $"End process request completed: pids={string.Join(", ", pids)}",
                    $"结束进程请求已完成：{string.Join(", ", pids)}",
                    log2Console: false);
            }
            catch (Exception exception)
            {
                StatusMessage = string.Format(CultureInfo.CurrentCulture, T(NexusDashL.StatusEndFailed), exception.Message);
                Logger.Error(
                    $"End process request failed: pids={string.Join(", ", pids)}",
                    exception,
                    StatusMessage,
                    log2Console: false);
            }
        }

        private static IReadOnlyList<double> AppendHistory(IReadOnlyList<double> history, double value)
        {
            const int capacity = 60;
            var nextValue = Math.Clamp(value, 0, 100);
            var nextCount = Math.Min(history.Count + 1, capacity);
            var next = new double[nextCount];
            var sourceCount = nextCount - 1;
            var sourceStart = Math.Max(history.Count - sourceCount, 0);

            for (var index = 0; index < sourceCount; index++)
            {
                next[index] = history[sourceStart + index];
            }

            next[^1] = nextValue;
            return next;
        }

        private void RaiseTerminationConfirmationProperties()
        {
            this.RaisePropertyChanged(nameof(EndProcessConfirmationTitleText));
            this.RaisePropertyChanged(nameof(EndProcessConfirmationMessageText));
            this.RaisePropertyChanged(nameof(EndProcessConfirmationProcessListText));
            this.RaisePropertyChanged(nameof(PendingTerminationCandidates));
            this.RaisePropertyChanged(nameof(PendingTerminationSelectedCount));
            this.RaisePropertyChanged(nameof(PendingTerminationTotalCount));
            this.RaisePropertyChanged(nameof(HasSelectedPendingTerminationProcesses));
            PublishEndProcessConfirmationState();
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

        private void InitializeProcessColumnWidths(UserPreferences preferences)
        {
            _processColumnWidths.Clear();
            if (preferences.ProcessColumnWidths is null)
            {
                return;
            }

            foreach (var (key, width) in preferences.ProcessColumnWidths)
            {
                if (ProcessTableSort.TryNormalizeColumnKey(key, out var normalizedKey) &&
                    width >= 32 &&
                    double.IsFinite(width))
                {
                    _processColumnWidths[normalizedKey] = Math.Round(width, 2);
                }
            }
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

            _userPreferencesService.Update(preferences =>
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

        private void SetLanguage(string? cultureName, bool showStatus = true, bool persist = true)
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
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            I18nManager.Instance.Culture = culture;
            if (persist)
            {
                _userPreferencesService.Update(preferences => preferences.CultureName = culture.Name);
            }

            App.ApplyThirdPartyCulture(culture.Name);

            RefreshLanguageOptions();
            RefreshLocalizedProperties();
            PublishSettingsState();
            QueueDeferredProcessLocalizationRefresh();

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
            this.RaisePropertyChanged(nameof(RememberWindowSizeText));
            this.RaisePropertyChanged(nameof(ProcessManagerText));
            this.RaisePropertyChanged(nameof(FileSearchToolText));
            this.RaisePropertyChanged(nameof(HardwareInfoToolText));
            this.RaisePropertyChanged(nameof(OperationLogText));
            this.RaisePropertyChanged(nameof(ProcessOverviewText));
            RefreshToolMenuHeaders();
            this.RaisePropertyChanged(nameof(ThemeMenuText));
            this.RaisePropertyChanged(nameof(DarkThemeText));
            this.RaisePropertyChanged(nameof(LightThemeText));
            this.RaisePropertyChanged(nameof(LanguageMenuText));
            this.RaisePropertyChanged(nameof(PauseText));
            this.RaisePropertyChanged(nameof(ResumeText));
            this.RaisePropertyChanged(nameof(SearchPlaceholderText));
            this.RaisePropertyChanged(nameof(SearchNoResultsText));
            this.RaisePropertyChanged(nameof(EndProcessText));
            this.RaisePropertyChanged(nameof(EndProcessTreeText));
            this.RaisePropertyChanged(nameof(EndAssociatedProcessesText));
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
            this.RaisePropertyChanged(nameof(FilterHasNetworkConnectionsText));
            this.RaisePropertyChanged(nameof(FilterHighCpuText));
            this.RaisePropertyChanged(nameof(FilterUserProcessesText));
            this.RaisePropertyChanged(nameof(FilterHideSystemProcessesText));
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
            this.RaisePropertyChanged(nameof(CopyLocalEndpointText));
            this.RaisePropertyChanged(nameof(CopyRemoteEndpointText));
            this.RaisePropertyChanged(nameof(CopyConnectionInfoText));
            this.RaisePropertyChanged(nameof(ExportSnapshotText));
            this.RaisePropertyChanged(nameof(ExportProcessListJsonText));
            this.RaisePropertyChanged(nameof(ExportProcessListCsvText));
            this.RaisePropertyChanged(nameof(ExportSelectedProcessJsonText));
            this.RaisePropertyChanged(nameof(ExportSelectedProcessCsvText));
            this.RaisePropertyChanged(nameof(StatusSnapshotExportedText));
            this.RaisePropertyChanged(nameof(StatusSnapshotExportFailedText));
            this.RaisePropertyChanged(nameof(StatusSelectedProcessSnapshotExportedText));
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
            this.RaisePropertyChanged(nameof(ActiveCountText));
            this.RaisePropertyChanged(nameof(ActiveStatusMessage));
            FileSearch.RefreshLocalizedText();
            HardwareInfo.RefreshLocalizedText();
            PublishProcessListState();
            PublishShellState();
        }

        private static string ResolveStartupCultureName(string? configuredCultureName)
        {
            return App.ResolveStartupCulture(configuredCultureName).Name;
        }

        private static string NormalizeCulture(string? cultureName)
        {
            return App.NormalizeCulture(cultureName);
        }

        private static string GetThemeDisplayName(string key)
        {
            return key switch
            {
                ThemeResourceService.SystemThemeKey => T(NexusDashL.ThemeSystem),
                ThemeResourceService.LightThemeKey => T(NexusDashL.LightTheme),
                ThemeResourceService.DarkThemeKey => T(NexusDashL.DarkTheme),
                ThemeResourceService.AquaticThemeKey => T(NexusDashL.ThemeAquatic),
                ThemeResourceService.DesertThemeKey => T(NexusDashL.ThemeDesert),
                ThemeResourceService.DuskThemeKey => T(NexusDashL.ThemeDusk),
                ThemeResourceService.NightSkyThemeKey => T(NexusDashL.ThemeNightSky),
                _ => key
            };
        }

        private static string T(string key)
        {
            return I18nManager.Instance.GetResource(key) ?? key;
        }

        private void ApplyApplicationTheme(string themeKey)
        {
            _themeResourceService.Apply(themeKey);
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

        private bool SetFilterField(ref bool field, bool value, string propertyName)
        {
            if (!SetField(ref field, value, propertyName))
            {
                return false;
            }

            this.RaisePropertyChanged(nameof(IsProcessFilterActive));
            this.RaisePropertyChanged(nameof(HasNoVisibleProcesses));
            this.RaisePropertyChanged(nameof(SearchNoResultsText));
            return true;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _refreshCancellation.Cancel();
            _eventBus.Unsubscribe(this);
            FileSearch.PropertyChanged -= HandleFileSearchPropertyChanged;
            HardwareInfo.PropertyChanged -= HandleHardwareInfoPropertyChanged;
            ToolTree.Dispose();
            ToolContent.Dispose();
            OperationLog.Dispose();
            StatusBar.Dispose();
            EndProcessConfirmation.Dispose();
            ProcessManager.Dispose();
            ProcessList.Dispose();
            FileSearch.Dispose();
            HardwareInfo.Dispose();
            _systemMonitorService.Dispose();
            _ = DisposeRefreshCancellationWhenIdleAsync();
        }

        private async Task DisposeRefreshCancellationWhenIdleAsync()
        {
            try
            {
                await _refreshLoopTask.ConfigureAwait(false);
            }
            catch
            {
            }

            _refreshCancellation.Dispose();
        }
    }
}
