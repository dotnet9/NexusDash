using CodeWF.EventBus;

namespace NexusDash.ViewModels
{
    public sealed class ToolContentViewModel : EventBusViewModel
    {
        private bool _isProcessToolSelected = true;
        private bool _isFileSearchToolSelected;
        private bool _isHardwareInfoToolSelected;

        public ToolContentViewModel(
            IEventBus eventBus,
            ProcessManagerViewModel processManager,
            FileSearchViewModel fileSearch,
            HardwareInfoViewModel hardwareInfo)
            : base(eventBus)
        {
            ProcessManager = processManager;
            FileSearch = fileSearch;
            HardwareInfo = hardwareInfo;
        }

        public ProcessManagerViewModel ProcessManager { get; }
        public FileSearchViewModel FileSearch { get; }
        public HardwareInfoViewModel HardwareInfo { get; }
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

        public bool IsHardwareInfoToolSelected
        {
            get => _isHardwareInfoToolSelected;
            private set => SetField(ref _isHardwareInfoToolSelected, value, nameof(IsHardwareInfoToolSelected));
        }

        [EventHandler]
        private void ApplyState(ActiveToolStateChangedCommand command)
        {
            IsProcessToolSelected = command.State.IsProcessToolSelected;
            IsFileSearchToolSelected = command.State.IsFileSearchToolSelected;
            IsHardwareInfoToolSelected = command.State.IsHardwareInfoToolSelected;
        }
    }
}
