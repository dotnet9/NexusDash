using CodeWF.EventBus;
using CodeWF.Log.Core;
using NexusDash.Services;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace NexusDash.ViewModels
{
    public sealed class StatusBarViewModel : EventBusViewModel
    {
        private readonly IProcessSnapshotExportService _snapshotExportService;
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
        private string _activeStatusMessage = "";
        private string _activeCountText = "";
        private bool _canShowPauseRefresh;
        private bool _canShowResumeRefresh;
        private bool _isProcessToolSelected = true;
        private bool _hasSelectedProcess;
        private int _processTotalCount;
        private ProcessRowViewModel? _selectedProcess;
        private IReadOnlyList<ProcessRowViewModel> _visibleProcesses = [];

        public StatusBarViewModel(
            IEventBus eventBus,
            IProcessSnapshotExportService snapshotExportService)
            : base(eventBus)
        {
            _snapshotExportService = snapshotExportService;
            PauseRefreshCommand = new DelegateCommand(() => EventBus.Publish(new PauseRefreshRequestedCommand()));
            ResumeRefreshCommand = new DelegateCommand(() => EventBus.Publish(new ResumeRefreshRequestedCommand()));
            ExportProcessListJsonCommand = new DelegateCommand(() =>
                _ = ExportProcessSnapshotAsync(ProcessSnapshotExportFormat.Json, ProcessSnapshotExportScope.ProcessList));
            ExportProcessListCsvCommand = new DelegateCommand(() =>
                _ = ExportProcessSnapshotAsync(ProcessSnapshotExportFormat.Csv, ProcessSnapshotExportScope.ProcessList));
            ExportSelectedProcessJsonCommand = new DelegateCommand(() =>
                _ = ExportProcessSnapshotAsync(ProcessSnapshotExportFormat.Json, ProcessSnapshotExportScope.SelectedProcess));
            ExportSelectedProcessCsvCommand = new DelegateCommand(() =>
                _ = ExportProcessSnapshotAsync(ProcessSnapshotExportFormat.Csv, ProcessSnapshotExportScope.SelectedProcess));
        }

        public DelegateCommand PauseRefreshCommand { get; }
        public DelegateCommand ResumeRefreshCommand { get; }
        public DelegateCommand ExportProcessListJsonCommand { get; }
        public DelegateCommand ExportProcessListCsvCommand { get; }
        public DelegateCommand ExportSelectedProcessJsonCommand { get; }
        public DelegateCommand ExportSelectedProcessCsvCommand { get; }

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
        public string ActiveStatusMessage { get => _activeStatusMessage; private set => SetField(ref _activeStatusMessage, value, nameof(ActiveStatusMessage)); }
        public string ActiveCountText { get => _activeCountText; private set => SetField(ref _activeCountText, value, nameof(ActiveCountText)); }
        public bool CanShowPauseRefresh { get => _canShowPauseRefresh; private set => SetField(ref _canShowPauseRefresh, value, nameof(CanShowPauseRefresh)); }
        public bool CanShowResumeRefresh { get => _canShowResumeRefresh; private set => SetField(ref _canShowResumeRefresh, value, nameof(CanShowResumeRefresh)); }
        public bool IsProcessToolSelected { get => _isProcessToolSelected; private set => SetField(ref _isProcessToolSelected, value, nameof(IsProcessToolSelected)); }
        public bool HasSelectedProcess { get => _hasSelectedProcess; private set => SetField(ref _hasSelectedProcess, value, nameof(HasSelectedProcess)); }
        public int ProcessTotalCount { get => _processTotalCount; private set => SetField(ref _processTotalCount, value, nameof(ProcessTotalCount)); }
        public ProcessRowViewModel? SelectedProcess { get => _selectedProcess; private set => SetField(ref _selectedProcess, value, nameof(SelectedProcess)); }
        public IReadOnlyList<ProcessRowViewModel> VisibleProcesses { get => _visibleProcesses; private set => SetField(ref _visibleProcesses, value, nameof(VisibleProcesses)); }

        public void ReportStatus(string message)
        {
            EventBus.Publish(new StatusMessageRequestedCommand(message));
        }

        private async Task ExportProcessSnapshotAsync(
            ProcessSnapshotExportFormat format,
            ProcessSnapshotExportScope scope)
        {
            try
            {
                var result = await _snapshotExportService.ExportAsync(CreateSnapshotExportState(), format, scope);
                if (!result.Exported)
                {
                    return;
                }

                ReportStatus(scope == ProcessSnapshotExportScope.SelectedProcess && result.SelectedProcessId is { } pid
                    ? string.Format(CultureInfo.CurrentCulture, StatusSelectedProcessSnapshotExportedText, pid)
                    : string.Format(CultureInfo.CurrentCulture, StatusSnapshotExportedText, result.RowCount));

                Logger.Info(
                    $"Exported process snapshot: scope={scope}, format={format}, rows={result.RowCount}, path={result.FilePath}",
                    $"Exported process snapshot: {result.RowCount} rows, {result.FilePath}",
                    log2Console: false);
            }
            catch (Exception exception)
            {
                var statusMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    StatusSnapshotExportFailedText,
                    exception.Message);
                ReportStatus(statusMessage);
                Logger.Error(
                    "Process snapshot export failed.",
                    exception,
                    statusMessage,
                    log2Console: false);
            }
        }

        private ProcessSnapshotExportState CreateSnapshotExportState()
        {
            return new ProcessSnapshotExportState(
                ProcessTotalCount,
                ExportSnapshotText,
                VisibleProcesses
                    .Where(static row => row.IsProcessRow)
                    .Select(CreateSnapshotRow)
                    .ToArray(),
                SelectedProcess is null ? null : CreateSnapshotRow(SelectedProcess));
        }

        private static ProcessSnapshotExportRow CreateSnapshotRow(ProcessRowViewModel row)
        {
            return new ProcessSnapshotExportRow(
                row.Pid,
                row.ParentPid,
                row.Name,
                row.RawName,
                row.Publisher,
                row.Category.ToString(),
                row.CpuPercent,
                row.WorkingSetBytes,
                row.DiskBytesPerSecond,
                row.TcpConnectionCount,
                row.UdpConnectionCount,
                row.NetworkConnectionCount,
                row.GpuPercent,
                row.ExecutablePath,
                row.CommandLine,
                row.StartTime,
                row.IsAccessDenied);
        }

        [EventHandler]
        private void ApplyState(StatusBarStateChangedCommand command)
        {
            var state = command.State;
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
            ActiveStatusMessage = state.ActiveStatusMessage;
            ActiveCountText = state.ActiveCountText;
            CanShowPauseRefresh = state.CanShowPauseRefresh;
            CanShowResumeRefresh = state.CanShowResumeRefresh;
            IsProcessToolSelected = state.IsProcessToolSelected;
            HasSelectedProcess = state.HasSelectedProcess;
            ProcessTotalCount = state.ProcessTotalCount;
            SelectedProcess = state.SelectedProcess;
            VisibleProcesses = state.VisibleProcesses;
        }
    }
}
