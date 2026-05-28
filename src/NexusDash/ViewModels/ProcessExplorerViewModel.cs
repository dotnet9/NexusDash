using CodeWF.EventBus;
using NexusDash.Controls.Models;
using System.Collections.Generic;

namespace NexusDash.ViewModels
{
    public sealed class ProcessExplorerViewModel : EventBusViewModel
    {
        private string _treemapText = "";
        private IReadOnlyList<TreemapItem> _treemapProcesses = [];

        public ProcessExplorerViewModel(IEventBus eventBus, ProcessListViewModel processList)
            : base(eventBus)
        {
            ProcessList = processList;
        }

        public ProcessListViewModel ProcessList { get; }
        public string TreemapText { get => _treemapText; private set => SetField(ref _treemapText, value, nameof(TreemapText)); }
        public IReadOnlyList<TreemapItem> TreemapProcesses { get => _treemapProcesses; private set => SetField(ref _treemapProcesses, value, nameof(TreemapProcesses)); }

        [EventHandler]
        private void ApplyState(ProcessExplorerStateChangedCommand command)
        {
            TreemapText = command.State.TreemapText;
            TreemapProcesses = command.State.TreemapProcesses;
        }
    }
}
