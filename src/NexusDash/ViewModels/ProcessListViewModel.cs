using CodeWF.EventBus;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private string _columnVisibilityText = "";
        private string _requiredColumnText = "";
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
            _selectedProcessPid = state.SelectedProcessPid;
            SyncCollection(VisibleProcesses, state.VisibleProcesses);
            SyncCollection(ProcessColumns, state.ProcessColumns);

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
            ColumnVisibilityText = state.ColumnVisibilityText;
            RequiredColumnText = state.RequiredColumnText;
            HasSelectedProcesses = state.HasSelectedProcesses;
            HasNoVisibleProcesses = state.HasNoVisibleProcesses;
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
