using CodeWF.EventBus;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace NexusDash.ViewModels
{
    public sealed class ProcessListViewModel : ReactiveObject, IDisposable
    {
        private readonly IEventBus _eventBus;
        private bool _isDisposed;
        private int? _selectedProcessPid;
        private string _processTreeText = "";
        private string _processCountText = "";
        private string _searchNoResultsText = "";
        private string _endProcessText = "";
        private string _endProcessTreeText = "";
        private string _endAssociatedProcessesText = "";
        private string _pidText = "";
        private string _parentPidText = "";
        private string _processNameText = "";
        private string _publisherText = "";
        private string _cpuText = "";
        private string _memoryText = "";
        private string _diskText = "";
        private string _networkColumnText = "";
        private string _gpuText = "";
        private string _accessLimitedText = "";
        private string _filterHasNetworkConnectionsText = "";
        private string _filterHighCpuText = "";
        private string _filterUserProcessesText = "";
        private string _filterHideSystemProcessesText = "";
        private string _columnVisibilityText = "";
        private string _requiredColumnText = "";
        private string _processSortColumnKey = MainWindowViewModel.ProcessColumnName;
        private ListSortDirection _processSortDirection = ListSortDirection.Ascending;
        private IReadOnlyDictionary<string, double> _processColumnWidths = new Dictionary<string, double>();
        private bool _isApplyingState;
        private bool _filterHasNetworkConnections;
        private bool _filterHighCpu;
        private bool _filterUserProcesses;
        private bool _filterHideSystemProcesses;
        private bool _hasSelectedProcesses;
        private bool _hasNoVisibleProcesses;

        public ProcessListViewModel(IEventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus.Subscribe(this);
        }

        public ObservableCollection<ProcessRowViewModel> VisibleProcesses { get; } = new();
        public ObservableCollection<ProcessColumnOptionViewModel> ProcessColumns { get; } = new();

        public string ProcessTreeText
        {
            get => _processTreeText;
            private set => this.RaiseAndSetIfChanged(ref _processTreeText, value);
        }

        public string ProcessCountText
        {
            get => _processCountText;
            private set => this.RaiseAndSetIfChanged(ref _processCountText, value);
        }

        public string SearchNoResultsText
        {
            get => _searchNoResultsText;
            private set => this.RaiseAndSetIfChanged(ref _searchNoResultsText, value);
        }

        public string EndProcessText
        {
            get => _endProcessText;
            private set => this.RaiseAndSetIfChanged(ref _endProcessText, value);
        }

        public string EndProcessTreeText
        {
            get => _endProcessTreeText;
            private set => this.RaiseAndSetIfChanged(ref _endProcessTreeText, value);
        }

        public string EndAssociatedProcessesText
        {
            get => _endAssociatedProcessesText;
            private set => this.RaiseAndSetIfChanged(ref _endAssociatedProcessesText, value);
        }

        public string PidText
        {
            get => _pidText;
            private set => this.RaiseAndSetIfChanged(ref _pidText, value);
        }

        public string ParentPidText
        {
            get => _parentPidText;
            private set => this.RaiseAndSetIfChanged(ref _parentPidText, value);
        }

        public string ProcessNameText
        {
            get => _processNameText;
            private set => this.RaiseAndSetIfChanged(ref _processNameText, value);
        }

        public string PublisherText
        {
            get => _publisherText;
            private set => this.RaiseAndSetIfChanged(ref _publisherText, value);
        }

        public string CpuText
        {
            get => _cpuText;
            private set => this.RaiseAndSetIfChanged(ref _cpuText, value);
        }

        public string MemoryText
        {
            get => _memoryText;
            private set => this.RaiseAndSetIfChanged(ref _memoryText, value);
        }

        public string DiskText
        {
            get => _diskText;
            private set => this.RaiseAndSetIfChanged(ref _diskText, value);
        }

        public string NetworkColumnText
        {
            get => _networkColumnText;
            private set => this.RaiseAndSetIfChanged(ref _networkColumnText, value);
        }

        public string GpuText
        {
            get => _gpuText;
            private set => this.RaiseAndSetIfChanged(ref _gpuText, value);
        }

        public string AccessLimitedText
        {
            get => _accessLimitedText;
            private set => this.RaiseAndSetIfChanged(ref _accessLimitedText, value);
        }

        public string FilterHasNetworkConnectionsText
        {
            get => _filterHasNetworkConnectionsText;
            private set => this.RaiseAndSetIfChanged(ref _filterHasNetworkConnectionsText, value);
        }

        public string FilterHighCpuText
        {
            get => _filterHighCpuText;
            private set => this.RaiseAndSetIfChanged(ref _filterHighCpuText, value);
        }

        public string FilterUserProcessesText
        {
            get => _filterUserProcessesText;
            private set => this.RaiseAndSetIfChanged(ref _filterUserProcessesText, value);
        }

        public string FilterHideSystemProcessesText
        {
            get => _filterHideSystemProcessesText;
            private set => this.RaiseAndSetIfChanged(ref _filterHideSystemProcessesText, value);
        }

        public string ColumnVisibilityText
        {
            get => _columnVisibilityText;
            private set => this.RaiseAndSetIfChanged(ref _columnVisibilityText, value);
        }

        public string RequiredColumnText
        {
            get => _requiredColumnText;
            private set => this.RaiseAndSetIfChanged(ref _requiredColumnText, value);
        }

        public string ProcessSortColumnKey
        {
            get => _processSortColumnKey;
            private set => this.RaiseAndSetIfChanged(ref _processSortColumnKey, value);
        }

        public ListSortDirection ProcessSortDirection
        {
            get => _processSortDirection;
            private set => this.RaiseAndSetIfChanged(ref _processSortDirection, value);
        }

        public IReadOnlyDictionary<string, double> ProcessColumnWidths
        {
            get => _processColumnWidths;
            private set => this.RaiseAndSetIfChanged(ref _processColumnWidths, value);
        }

        public bool FilterHasNetworkConnections
        {
            get => _filterHasNetworkConnections;
            set => SetFilter(ref _filterHasNetworkConnections, value, nameof(FilterHasNetworkConnections), MainWindowViewModel.ProcessFilterHasNetworkConnections);
        }

        public bool FilterHighCpu
        {
            get => _filterHighCpu;
            set => SetFilter(ref _filterHighCpu, value, nameof(FilterHighCpu), MainWindowViewModel.ProcessFilterHighCpu);
        }

        public bool FilterUserProcesses
        {
            get => _filterUserProcesses;
            set => SetFilter(ref _filterUserProcesses, value, nameof(FilterUserProcesses), MainWindowViewModel.ProcessFilterUserProcesses);
        }

        public bool FilterHideSystemProcesses
        {
            get => _filterHideSystemProcesses;
            set => SetFilter(ref _filterHideSystemProcesses, value, nameof(FilterHideSystemProcesses), MainWindowViewModel.ProcessFilterHideSystemProcesses);
        }

        public bool HasSelectedProcesses
        {
            get => _hasSelectedProcesses;
            private set => this.RaiseAndSetIfChanged(ref _hasSelectedProcesses, value);
        }

        public bool HasNoVisibleProcesses
        {
            get => _hasNoVisibleProcesses;
            private set => this.RaiseAndSetIfChanged(ref _hasNoVisibleProcesses, value);
        }

        public void EndSelectedProcesses()
        {
            _eventBus.Publish(new ProcessTerminationRequestedCommand(entireProcessTree: false));
        }

        public void EndSelectedProcessTrees()
        {
            _eventBus.Publish(new ProcessTerminationRequestedCommand(entireProcessTree: true));
        }

        public void EndSelectedAssociatedProcesses()
        {
            _eventBus.Publish(new ProcessTerminationRequestedCommand(
                entireProcessTree: false,
                includeAssociatedProcesses: true));
        }

        public void SetSelectedProcesses(IEnumerable<ProcessRowViewModel> selectedRows)
        {
            _eventBus.Publish(new ProcessListSelectionChangedCommand(
                selectedRows.Where(static row => !row.IsGroupHeader).ToArray()));
        }

        public bool IsSelectedProcessStillVisible()
        {
            return _selectedProcessPid is { } pid &&
                   VisibleProcesses.Any(row => !row.IsGroupHeader && row.Pid == pid);
        }

        public bool IsProcessColumnVisible(string key)
        {
            return ProcessColumns.FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
                is not { } option || option.IsVisible;
        }

        public void SetProcessColumnVisibility(string key, bool isVisible)
        {
            _eventBus.Publish(new ProcessColumnVisibilityChangedCommand(key, isVisible));
        }

        public void SetProcessColumnWidth(string key, double width)
        {
            _eventBus.Publish(new ProcessColumnWidthChangedCommand(key, width));
        }

        public void SetProcessSort(string columnKey)
        {
            _eventBus.Publish(new ProcessSortChangedCommand(columnKey));
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _eventBus.Unsubscribe(this);
            _isDisposed = true;
        }

        [EventHandler]
        private void ApplyState(ProcessListStateChangedCommand command)
        {
            var state = command.State;
            _isApplyingState = true;
            try
            {
                _selectedProcessPid = state.SelectedProcessPid;
                ProcessSortColumnKey = state.ProcessSortColumnKey;
                ProcessSortDirection = state.ProcessSortDirection;
                ProcessColumnWidths = new Dictionary<string, double>(
                    state.ProcessColumnWidths,
                    StringComparer.OrdinalIgnoreCase);
                FilterHasNetworkConnections = state.FilterHasNetworkConnections;
                FilterHighCpu = state.FilterHighCpu;
                FilterUserProcesses = state.FilterUserProcesses;
                FilterHideSystemProcesses = state.FilterHideSystemProcesses;
                SyncCollection(VisibleProcesses, state.VisibleProcesses);
                SyncCollection(ProcessColumns, state.ProcessColumns);
            }
            finally
            {
                _isApplyingState = false;
            }

            ProcessTreeText = state.ProcessTreeText;
            ProcessCountText = state.ProcessCountText;
            SearchNoResultsText = state.SearchNoResultsText;
            EndProcessText = state.EndProcessText;
            EndProcessTreeText = state.EndProcessTreeText;
            EndAssociatedProcessesText = state.EndAssociatedProcessesText;
            PidText = state.PidText;
            ParentPidText = state.ParentPidText;
            ProcessNameText = state.ProcessNameText;
            PublisherText = state.PublisherText;
            CpuText = state.CpuText;
            MemoryText = state.MemoryText;
            DiskText = state.DiskText;
            NetworkColumnText = state.NetworkColumnText;
            GpuText = state.GpuText;
            AccessLimitedText = state.AccessLimitedText;
            FilterHasNetworkConnectionsText = state.FilterHasNetworkConnectionsText;
            FilterHighCpuText = state.FilterHighCpuText;
            FilterUserProcessesText = state.FilterUserProcessesText;
            FilterHideSystemProcessesText = state.FilterHideSystemProcessesText;
            ColumnVisibilityText = state.ColumnVisibilityText;
            RequiredColumnText = state.RequiredColumnText;
            HasSelectedProcesses = state.HasSelectedProcesses;
            HasNoVisibleProcesses = state.HasNoVisibleProcesses;
        }

        private void SetFilter(ref bool field, bool value, string propertyName, string filterKey)
        {
            if (field == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref field, value, propertyName);
            if (_isApplyingState)
            {
                return;
            }

            _eventBus.Publish(new ProcessFilterChangedCommand(filterKey, value));
        }

        private static void SyncCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
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
    }
}
