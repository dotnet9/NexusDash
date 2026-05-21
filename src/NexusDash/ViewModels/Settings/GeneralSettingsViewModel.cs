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
        public string RefreshIntervalText => MainViewModel.RefreshIntervalText;
        public bool IsOneSecondRefreshInterval => MainViewModel.RefreshIntervalSeconds == 1;
        public bool IsTwoSecondRefreshInterval => MainViewModel.RefreshIntervalSeconds == 2;
        public bool IsFiveSecondRefreshInterval => MainViewModel.RefreshIntervalSeconds == 5;

        public void SetOneSecondRefreshInterval()
        {
            MainViewModel.SetRefreshIntervalSeconds(1);
        }

        public void SetTwoSecondRefreshInterval()
        {
            MainViewModel.SetRefreshIntervalSeconds(2);
        }

        public void SetFiveSecondRefreshInterval()
        {
            MainViewModel.SetRefreshIntervalSeconds(5);
        }

        protected override void RaiseLocalizedProperties()
        {
            this.RaisePropertyChanged(nameof(Header));
            this.RaisePropertyChanged(nameof(RealtimeMonitoringLabel));
            this.RaisePropertyChanged(nameof(RealtimeMonitoringValue));
            this.RaisePropertyChanged(nameof(RefreshCadenceLabel));
            this.RaisePropertyChanged(nameof(RefreshCadenceValue));
            this.RaisePropertyChanged(nameof(RefreshIntervalText));
            this.RaisePropertyChanged(nameof(IsOneSecondRefreshInterval));
            this.RaisePropertyChanged(nameof(IsTwoSecondRefreshInterval));
            this.RaisePropertyChanged(nameof(IsFiveSecondRefreshInterval));
        }
    }
}
