using CodeWF.EventBus;
using NexusDash.ViewModels.Settings;

namespace NexusDash.ViewModels
{
    public sealed class ToolContentViewModel : EventBusViewModel
    {
        private bool _isProcessToolSelected = true;
        private bool _isFileSearchToolSelected;
        private bool _isHardwareInfoToolSelected;
        private bool _isSettingsToolSelected;
        private object _activeTool;

        public ToolContentViewModel(
            IEventBus eventBus,
            ProcessManagerViewModel processManager,
            FileSearchViewModel fileSearch,
            HardwareInfoViewModel hardwareInfo,
            SettingsViewModel settings)
            : base(eventBus)
        {
            ProcessManager = processManager;
            FileSearch = fileSearch;
            HardwareInfo = hardwareInfo;
            Settings = settings;
            _activeTool = processManager;
        }

        public ProcessManagerViewModel ProcessManager { get; }
        public FileSearchViewModel FileSearch { get; }
        public HardwareInfoViewModel HardwareInfo { get; }
        public SettingsViewModel Settings { get; }
        public object ActiveTool
        {
            get => _activeTool;
            private set => SetField(ref _activeTool, value, nameof(ActiveTool));
        }

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

        public bool IsSettingsToolSelected
        {
            get => _isSettingsToolSelected;
            private set => SetField(ref _isSettingsToolSelected, value, nameof(IsSettingsToolSelected));
        }

        public override void Dispose()
        {
            Settings.Dispose();
            base.Dispose();
        }

        [EventHandler]
        private void ApplyState(ActiveToolStateChangedCommand command)
        {
            IsProcessToolSelected = command.State.IsProcessToolSelected;
            IsFileSearchToolSelected = command.State.IsFileSearchToolSelected;
            IsHardwareInfoToolSelected = command.State.IsHardwareInfoToolSelected;
            IsSettingsToolSelected = command.State.IsSettingsToolSelected;
            ActiveTool = IsSettingsToolSelected
                ? Settings
                : IsFileSearchToolSelected
                ? FileSearch
                : IsHardwareInfoToolSelected
                    ? HardwareInfo
                    : ProcessManager;
        }
    }
}
