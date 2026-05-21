using NexusDash;
using ReactiveUI;

namespace NexusDash.ViewModels.Settings
{
    public sealed class NetworkSettingsViewModel(MainWindowViewModel mainViewModel) : SettingsPageViewModelBase(mainViewModel)
    {
        public override string Header => T(NexusDashL.SettingsNetwork);
        public string NetworkMappingLabel => T(NexusDashL.SettingsNetworkMapping);
        public string NetworkMappingValue => T(NexusDashL.SettingsNetworkMappingValue);
        public string NetworkRefreshLabel => T(NexusDashL.SettingsNetworkRefresh);
        public string NetworkRefreshValue => T(NexusDashL.SettingsNetworkRefreshValue);

        protected override void RaiseLocalizedProperties()
        {
            this.RaisePropertyChanged(nameof(Header));
            this.RaisePropertyChanged(nameof(NetworkMappingLabel));
            this.RaisePropertyChanged(nameof(NetworkMappingValue));
            this.RaisePropertyChanged(nameof(NetworkRefreshLabel));
            this.RaisePropertyChanged(nameof(NetworkRefreshValue));
        }
    }
}
