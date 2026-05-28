using CodeWF.EventBus;

namespace NexusDash.ViewModels
{
    public sealed class ToolContentViewModel : EventBusViewModel
    {
        private bool _isProcessToolSelected = true;
        private bool _isFileSearchToolSelected;

        public ToolContentViewModel(
            IEventBus eventBus,
            ProcessManagerViewModel processManager,
            FileSearchViewModel fileSearch)
            : base(eventBus)
        {
            ProcessManager = processManager;
            FileSearch = fileSearch;
        }

        public ProcessManagerViewModel ProcessManager { get; }
        public FileSearchViewModel FileSearch { get; }
        public bool IsProcessToolSelected
        {
            get => _isProcessToolSelected;
            private set => SetField(ref _isProcessToolSelected, value, nameof(IsProcessToolSelected));
        }

        public bool IsFileSearchToolSelected
        {
            get => _isFileSearchToolSelected;
            private set => SetField(ref _isFileSearchToolSelected, value, nameof(IsFileSearchToolSelected));
        }

        [EventHandler]
        private void ApplyState(ActiveToolStateChangedCommand command)
        {
            IsProcessToolSelected = command.State.IsProcessToolSelected;
            IsFileSearchToolSelected = command.State.IsFileSearchToolSelected;
        }
    }
}
