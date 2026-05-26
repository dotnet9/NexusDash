using CodeWF.EventBus;
using System.Collections.Generic;
using System.ComponentModel;

namespace NexusDash.ViewModels
{
    public sealed class ProcessListState
    {
        public IReadOnlyList<ProcessRowViewModel> VisibleProcesses { get; init; } = [];
        public IReadOnlyList<ProcessColumnOptionViewModel> ProcessColumns { get; init; } = [];
        public int? SelectedProcessPid { get; init; }
        public string ProcessSortColumnKey { get; init; } = "name";
        public ListSortDirection ProcessSortDirection { get; init; } = ListSortDirection.Ascending;
        public string ProcessTreeText { get; init; } = "";
        public string ProcessCountText { get; init; } = "";
        public string SearchNoResultsText { get; init; } = "";
        public string EndProcessText { get; init; } = "";
        public string EndProcessTreeText { get; init; } = "";
        public string EndAssociatedProcessesText { get; init; } = "";
        public string PidText { get; init; } = "";
        public string ParentPidText { get; init; } = "";
        public string ProcessNameText { get; init; } = "";
        public string PublisherText { get; init; } = "";
        public string CpuText { get; init; } = "";
        public string MemoryText { get; init; } = "";
        public string DiskText { get; init; } = "";
        public string NetworkColumnText { get; init; } = "";
        public string GpuText { get; init; } = "";
        public string AccessLimitedText { get; init; } = "";
        public string ColumnVisibilityText { get; init; } = "";
        public string RequiredColumnText { get; init; } = "";
        public bool HasSelectedProcesses { get; init; }
        public bool HasNoVisibleProcesses { get; init; }
    }

    public sealed class ProcessListStateChangedCommand(ProcessListState state) : Command
    {
        public ProcessListState State { get; } = state;
    }

    public sealed class ProcessListSelectionChangedCommand(IReadOnlyList<ProcessRowViewModel> selectedRows) : Command
    {
        public IReadOnlyList<ProcessRowViewModel> SelectedRows { get; } = selectedRows;
    }

    public sealed class ProcessTerminationRequestedCommand(
        bool entireProcessTree,
        bool includeAssociatedProcesses = false) : Command
    {
        public bool EntireProcessTree { get; } = entireProcessTree;
        public bool IncludeAssociatedProcesses { get; } = includeAssociatedProcesses;
    }

    public sealed class ProcessColumnVisibilityChangedCommand(string key, bool isVisible) : Command
    {
        public string Key { get; } = key;
        public bool IsVisible { get; } = isVisible;
    }

    public sealed class ProcessSortChangedCommand(string columnKey) : Command
    {
        public string ColumnKey { get; } = columnKey;
    }
}
