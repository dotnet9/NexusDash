using NexusDash;
using ReactiveUI;

namespace NexusDash.ViewModels.Settings
{
    public sealed class ProcessSettingsViewModel(MainWindowViewModel mainViewModel) : SettingsPageViewModelBase(mainViewModel)
    {
        public override string Header => T(NexusDashL.SettingsProcesses);
        public string ProcessGroupingLabel => T(NexusDashL.SettingsProcessGrouping);
        public string ProcessGroupingValue => T(NexusDashL.SettingsProcessGroupingValue);
        public string ProcessIconsLabel => T(NexusDashL.SettingsProcessIcons);
        public string ProcessIconsValue => T(NexusDashL.SettingsProcessIconsValue);
        public string ColumnPersistenceLabel => T(NexusDashL.SettingsColumnPersistence);
        public string ColumnPersistenceValue => T(NexusDashL.SettingsColumnPersistenceValue);

        protected override void RaiseLocalizedProperties()
        {
            this.RaisePropertyChanged(nameof(Header));
            this.RaisePropertyChanged(nameof(ProcessGroupingLabel));
            this.RaisePropertyChanged(nameof(ProcessGroupingValue));
            this.RaisePropertyChanged(nameof(ProcessIconsLabel));
            this.RaisePropertyChanged(nameof(ProcessIconsValue));
            this.RaisePropertyChanged(nameof(ColumnPersistenceLabel));
            this.RaisePropertyChanged(nameof(ColumnPersistenceValue));
        }
    }
}
