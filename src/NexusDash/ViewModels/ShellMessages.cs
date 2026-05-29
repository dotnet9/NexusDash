using CodeWF.EventBus;
using NexusDash.Controls.Models;
using NexusDash.Models;
using System.Collections.Generic;

namespace NexusDash.ViewModels
{
    public sealed class ToolTreeState
    {
        public IReadOnlyList<ToolMenuNode> ToolMenuItems { get; init; } = [];
        public ToolMenuNode? SelectedToolNode { get; init; }
    }

    public sealed class ActiveToolState
    {
        public bool IsProcessToolSelected { get; init; }
        public bool IsFileSearchToolSelected { get; init; }
        public bool IsHardwareInfoToolSelected { get; init; }
        public bool IsSettingsToolSelected { get; init; }
    }

    public sealed class ProcessManagerState
    {
        public string ProcessOverviewText { get; init; } = "";
        public string ProcessTreeText { get; init; } = "";
        public string DetailsText { get; init; } = "";
    }

    public sealed class ProcessOverviewState
    {
        public string CpuText { get; init; } = "";
        public string MemoryText { get; init; } = "";
        public string DiskText { get; init; } = "";
        public string NetworkText { get; init; } = "";
        public string CpuUsageText { get; init; } = "";
        public string MemoryUsageText { get; init; } = "";
        public string MemorySummaryText { get; init; } = "";
        public string DiskSpeedText { get; init; } = "";
        public string NetworkSpeedText { get; init; } = "";
        public string TopCpuProcessText { get; init; } = "";
        public string TopMemoryProcessText { get; init; } = "";
        public string TopDiskProcessText { get; init; } = "";
        public string TopNetworkProcessText { get; init; } = "";
        public double CpuUsage { get; init; }
        public IReadOnlyList<double> CpuHistory { get; init; } = [];
        public IReadOnlyList<double> MemoryHistory { get; init; } = [];
        public IReadOnlyList<double> DiskHistory { get; init; } = [];
        public IReadOnlyList<double> NetworkHistory { get; init; } = [];
    }

    public sealed class ProcessExplorerState
    {
        public string TreemapText { get; init; } = "";
        public IReadOnlyList<TreemapItem> TreemapProcesses { get; init; } = [];
    }

    public sealed class ProcessInspectorState
    {
        public string DetailsText { get; init; } = "";
        public string HandlesText { get; init; } = "";
        public string NetworkText { get; init; } = "";
        public string ServicesText { get; init; } = "";
        public string StartupText { get; init; } = "";
        public string NoProcessSelectedText { get; init; } = "";
        public string AccessLimitedText { get; init; } = "";
        public string AccessLimitedDescriptionText { get; init; } = "";
        public string PidText { get; init; } = "";
        public string PublisherText { get; init; } = "";
        public string StartTimeText { get; init; } = "";
        public string CpuText { get; init; } = "";
        public string MemoryText { get; init; } = "";
        public string PathText { get; init; } = "";
        public string CommandLineText { get; init; } = "";
        public string HandlesSearchPlaceholderText { get; init; } = "";
        public string HandlesUnavailableText { get; init; } = "";
        public string ServicesUnavailableText { get; init; } = "";
        public string StartupUnavailableText { get; init; } = "";
        public string ProcessNetworkConnectionsText { get; init; } = "";
        public string SelectedProcessNetworkSummaryText { get; init; } = "";
        public string SelectedProcessConnectionTotalText { get; init; } = "";
        public string SelectedProcessTcpConnectionCountText { get; init; } = "";
        public string SelectedProcessUdpConnectionCountText { get; init; } = "";
        public string NetworkSelectProcessText { get; init; } = "";
        public string NetworkNoConnectionsText { get; init; } = "";
        public string ProtocolText { get; init; } = "";
        public string LocalEndpointText { get; init; } = "";
        public string RemoteEndpointText { get; init; } = "";
        public string StateText { get; init; } = "";
        public string LastSeenText { get; init; } = "";
        public string CopyLocalEndpointText { get; init; } = "";
        public string CopyRemoteEndpointText { get; init; } = "";
        public string CopyConnectionInfoText { get; init; } = "";
        public ProcessRowViewModel? SelectedProcess { get; init; }
        public bool HasSelectedProcess { get; init; }
        public bool HasSelectedProcessAccessLimit { get; init; }
        public bool HasSelectedProcessNetworkConnections { get; init; }
        public bool HasSelectedProcessWithoutNetworkConnections { get; init; }
        public IReadOnlyList<ProcessNetworkConnection> SelectedProcessNetworkConnections { get; init; } = [];
    }

    public sealed class StatusBarState
    {
        public string PauseText { get; init; } = "";
        public string ResumeText { get; init; } = "";
        public string ExportSnapshotText { get; init; } = "";
        public string ExportProcessListJsonText { get; init; } = "";
        public string ExportProcessListCsvText { get; init; } = "";
        public string ExportSelectedProcessJsonText { get; init; } = "";
        public string ExportSelectedProcessCsvText { get; init; } = "";
        public string StatusSnapshotExportedText { get; init; } = "";
        public string StatusSnapshotExportFailedText { get; init; } = "";
        public string StatusSelectedProcessSnapshotExportedText { get; init; } = "";
        public string ActiveStatusMessage { get; init; } = "";
        public string ActiveCountText { get; init; } = "";
        public bool CanShowPauseRefresh { get; init; }
        public bool CanShowResumeRefresh { get; init; }
        public bool IsProcessToolSelected { get; init; }
        public bool HasSelectedProcess { get; init; }
        public int ProcessTotalCount { get; init; }
        public ProcessRowViewModel? SelectedProcess { get; init; }
        public IReadOnlyList<ProcessRowViewModel> VisibleProcesses { get; init; } = [];
    }

    public sealed class EndProcessConfirmationState
    {
        public bool IsEndProcessConfirmationVisible { get; init; }
        public string EndProcessConfirmationTitleText { get; init; } = "";
        public string EndProcessConfirmationMessageText { get; init; } = "";
        public IReadOnlyList<ProcessTerminationCandidateViewModel> PendingTerminationCandidates { get; init; } = [];
        public bool HasSelectedPendingTerminationProcesses { get; init; }
        public string CancelText { get; init; } = "";
        public string ConfirmText { get; init; } = "";
    }

    public sealed class OperationLogState
    {
        public string OperationLogText { get; init; } = "";
        public string OperationLogContent { get; init; } = "";
    }

    public sealed class ToolTreeStateChangedCommand(ToolTreeState state) : Command
    {
        public ToolTreeState State { get; } = state;
    }

    public sealed class ActiveToolStateChangedCommand(ActiveToolState state) : Command
    {
        public ActiveToolState State { get; } = state;
    }

    public sealed class ProcessManagerStateChangedCommand(ProcessManagerState state) : Command
    {
        public ProcessManagerState State { get; } = state;
    }

    public sealed class ProcessOverviewStateChangedCommand(ProcessOverviewState state) : Command
    {
        public ProcessOverviewState State { get; } = state;
    }

    public sealed class ProcessExplorerStateChangedCommand(ProcessExplorerState state) : Command
    {
        public ProcessExplorerState State { get; } = state;
    }

    public sealed class ProcessInspectorStateChangedCommand(ProcessInspectorState state) : Command
    {
        public ProcessInspectorState State { get; } = state;
    }

    public sealed class StatusBarStateChangedCommand(StatusBarState state) : Command
    {
        public StatusBarState State { get; } = state;
    }

    public sealed class EndProcessConfirmationStateChangedCommand(EndProcessConfirmationState state) : Command
    {
        public EndProcessConfirmationState State { get; } = state;
    }

    public sealed class OperationLogStateChangedCommand(OperationLogState state) : Command
    {
        public OperationLogState State { get; } = state;
    }

    public sealed class ToolSelectionRequestedCommand(string toolKey) : Command
    {
        public string ToolKey { get; } = toolKey;
    }

    public sealed class PauseRefreshRequestedCommand : Command
    {
    }

    public sealed class ResumeRefreshRequestedCommand : Command
    {
    }

    public sealed class CancelPendingProcessTerminationCommand : Command
    {
    }

    public sealed class ConfirmPendingProcessTerminationCommand : Command
    {
    }

    public sealed class RememberWindowSizeChangedCommand(bool isEnabled) : Command
    {
        public bool IsEnabled { get; } = isEnabled;
    }

    public sealed class StatusMessageRequestedCommand(string message) : Command
    {
        public string Message { get; } = message;
    }
}
