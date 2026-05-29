using ReactiveUI;

namespace NexusDash.ViewModels
{
    public enum ToolMenuIcon
    {
        ProcessManager,
        FileSearch,
        HardwareInfo,
        Settings
    }

    public sealed class ToolMenuNode(string header, string toolKey, ToolMenuIcon icon) : ReactiveObject
    {
        private string _header = header;

        public string Header
        {
            get => _header;
            set => this.RaiseAndSetIfChanged(ref _header, value);
        }

        public string ToolKey { get; } = toolKey;
        public ToolMenuIcon Icon { get; } = icon;
        public bool ShowsProcessManagerIcon => Icon == ToolMenuIcon.ProcessManager;
        public bool ShowsFileSearchIcon => Icon == ToolMenuIcon.FileSearch;
        public bool ShowsHardwareInfoIcon => Icon == ToolMenuIcon.HardwareInfo;
        public bool ShowsSettingsIcon => Icon == ToolMenuIcon.Settings;
    }
}
