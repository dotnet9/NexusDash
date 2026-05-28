using CodeWF.EventBus;
using Prism.Commands;
using System.Collections.Generic;

namespace NexusDash.ViewModels
{
    public sealed class StatusBarViewModel : EventBusViewModel
    {
        private string _settingsText = "";
        private string _pauseText = "";
        private string _resumeText = "";
        private string _exportSnapshotText = "";
        private string _exportProcessListJsonText = "";
        private string _exportProcessListCsvText = "";
        private string _exportSelectedProcessJsonText = "";
        private string _exportSelectedProcessCsvText = "";
        private string _statusSnapshotExportedText = "";
        private string _statusSnapshotExportFailedText = "";
        private string _statusSelectedProcessSnapshotExportedText = "";
        private string _rememberWindowSizeText = "";
        private string _activeStatusMessage = "";
        private string _activeCountText = "";
        private bool _canShowPauseRefresh;
        private bool _canShowResumeRefresh;
        private bool _isProcessToolSelected = true;
        private bool _hasSelectedProcess;
        private bool _rememberWindowSize;
        private int _processTotalCount;
        private ProcessRowViewModel? _selectedProcess;
        private IReadOnlyList<ProcessRowViewModel> _visibleProcesses = [];

        public StatusBarViewModel(IEventBus eventBus)
            : base(eventBus)
        {
            OpenSettingsWindowCommand = new DelegateCommand(() => EventBus.Publish(new OpenSettingsWindowCommand()));
            PauseRefreshCommand = new DelegateCommand(() => EventBus.Publish(new PauseRefreshRequestedCommand()));
            ResumeRefreshCommand = new DelegateCommand(() => EventBus.Publish(new ResumeRefreshRequestedCommand()));
        }

        public DelegateCommand OpenSettingsWindowCommand { get; }
        public DelegateCommand PauseRefreshCommand { get; }
        public DelegateCommand ResumeRefreshCommand { get; }

        public string SettingsText { get => _settingsText; private set => SetField(ref _settingsText, value, nameof(SettingsText)); }
        public string PauseText { get => _pauseText; private set => SetField(ref _pauseText, value, nameof(PauseText)); }
        public string ResumeText { get => _resumeText; private set => SetField(ref _resumeText, value, nameof(ResumeText)); }
        public string ExportSnapshotText { get => _exportSnapshotText; private set => SetField(ref _exportSnapshotText, value, nameof(ExportSnapshotText)); }
        public string ExportProcessListJsonText { get => _exportProcessListJsonText; private set => SetField(ref _exportProcessListJsonText, value, nameof(ExportProcessListJsonText)); }
        public string ExportProcessListCsvText { get => _exportProcessListCsvText; private set => SetField(ref _exportProcessListCsvText, value, nameof(ExportProcessListCsvText)); }
        public string ExportSelectedProcessJsonText { get => _exportSelectedProcessJsonText; private set => SetField(ref _exportSelectedProcessJsonText, value, nameof(ExportSelectedProcessJsonText)); }
        public string ExportSelectedProcessCsvText { get => _exportSelectedProcessCsvText; private set => SetField(ref _exportSelectedProcessCsvText, value, nameof(ExportSelectedProcessCsvText)); }
        public string StatusSnapshotExportedText { get => _statusSnapshotExportedText; private set => SetField(ref _statusSnapshotExportedText, value, nameof(StatusSnapshotExportedText)); }
        public string StatusSnapshotExportFailedText { get => _statusSnapshotExportFailedText; private set => SetField(ref _statusSnapshotExportFailedText, value, nameof(StatusSnapshotExportFailedText)); }
        public string StatusSelectedProcessSnapshotExportedText { get => _statusSelectedProcessSnapshotExportedText; private set => SetField(ref _statusSelectedProcessSnapshotExportedText, value, nameof(StatusSelectedProcessSnapshotExportedText)); }
        public string RememberWindowSizeText { get => _rememberWindowSizeText; private set => SetField(ref _rememberWindowSizeText, value, nameof(RememberWindowSizeText)); }
        public string ActiveStatusMessage { get => _activeStatusMessage; private set => SetField(ref _activeStatusMessage, value, nameof(ActiveStatusMessage)); }
        public string ActiveCountText { get => _activeCountText; private set => SetField(ref _activeCountText, value, nameof(ActiveCountText)); }
        public bool CanShowPauseRefresh { get => _canShowPauseRefresh; private set => SetField(ref _canShowPauseRefresh, value, nameof(CanShowPauseRefresh)); }
        public bool CanShowResumeRefresh { get => _canShowResumeRefresh; private set => SetField(ref _canShowResumeRefresh, value, nameof(CanShowResumeRefresh)); }
        public bool IsProcessToolSelected { get => _isProcessToolSelected; private set => SetField(ref _isProcessToolSelected, value, nameof(IsProcessToolSelected)); }
        public bool HasSelectedProcess { get => _hasSelectedProcess; private set => SetField(ref _hasSelectedProcess, value, nameof(HasSelectedProcess)); }
        public bool RememberWindowSize { get => _rememberWindowSize; private set => SetField(ref _rememberWindowSize, value, nameof(RememberWindowSize)); }
        public int ProcessTotalCount { get => _processTotalCount; private set => SetField(ref _processTotalCount, value, nameof(ProcessTotalCount)); }
        public ProcessRowViewModel? SelectedProcess { get => _selectedProcess; private set => SetField(ref _selectedProcess, value, nameof(SelectedProcess)); }
        public IReadOnlyList<ProcessRowViewModel> VisibleProcesses { get => _visibleProcesses; private set => SetField(ref _visibleProcesses, value, nameof(VisibleProcesses)); }

        public void SetRememberWindowSize(bool isEnabled)
        {
            EventBus.Publish(new RememberWindowSizeChangedCommand(isEnabled));
        }

        public void ReportStatus(string message)
        {
            EventBus.Publish(new StatusMessageRequestedCommand(message));
        }

        [EventHandler]
        private void ApplyState(StatusBarStateChangedCommand command)
        {
            var state = command.State;
            SettingsText = state.SettingsText;
            PauseText = state.PauseText;
            ResumeText = state.ResumeText;
            ExportSnapshotText = state.ExportSnapshotText;
            ExportProcessListJsonText = state.ExportProcessListJsonText;
            ExportProcessListCsvText = state.ExportProcessListCsvText;
            ExportSelectedProcessJsonText = state.ExportSelectedProcessJsonText;
            ExportSelectedProcessCsvText = state.ExportSelectedProcessCsvText;
            StatusSnapshotExportedText = state.StatusSnapshotExportedText;
            StatusSnapshotExportFailedText = state.StatusSnapshotExportFailedText;
            StatusSelectedProcessSnapshotExportedText = state.StatusSelectedProcessSnapshotExportedText;
            RememberWindowSizeText = state.RememberWindowSizeText;
            ActiveStatusMessage = state.ActiveStatusMessage;
            ActiveCountText = state.ActiveCountText;
            CanShowPauseRefresh = state.CanShowPauseRefresh;
            CanShowResumeRefresh = state.CanShowResumeRefresh;
            IsProcessToolSelected = state.IsProcessToolSelected;
            HasSelectedProcess = state.HasSelectedProcess;
            RememberWindowSize = state.RememberWindowSize;
            ProcessTotalCount = state.ProcessTotalCount;
            SelectedProcess = state.SelectedProcess;
            VisibleProcesses = state.VisibleProcesses;
        }
    }
}
