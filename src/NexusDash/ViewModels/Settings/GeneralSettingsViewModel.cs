using NexusDash;
using ReactiveUI;

namespace NexusDash.ViewModels.Settings
{
    public sealed class GeneralSettingsViewModel(MainWindowViewModel mainViewModel) : SettingsPageViewModelBase(mainViewModel)
    {
        public override string Header => T(NexusDashL.SettingsGeneral);
        public string RealtimeMonitoringLabel => T(NexusDashL.SettingsRealtimeMonitoring);
        public string RealtimeMonitoringValue => T(NexusDashL.SettingsRealtimeMonitoringValue);
        public string RefreshCadenceLabel => T(NexusDashL.SettingsRefreshCadence);
        public string RefreshCadenceValue => T(NexusDashL.SettingsRefreshCadenceValue);

        protected override void RaiseLocalizedProperties()
        {
            this.RaisePropertyChanged(nameof(Header));
            this.RaisePropertyChanged(nameof(RealtimeMonitoringLabel));
            this.RaisePropertyChanged(nameof(RealtimeMonitoringValue));
            this.RaisePropertyChanged(nameof(RefreshCadenceLabel));
            this.RaisePropertyChanged(nameof(RefreshCadenceValue));
        }
    }
}
