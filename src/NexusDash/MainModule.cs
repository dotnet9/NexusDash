using NexusDash.ViewModels;
using NexusDash.ViewModels.Settings;
using NexusDash.Views.Settings;
using NexusDash.Regions;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Mvvm;
using Prism.Regions;

namespace NexusDash
{
    public sealed class MainModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var regionManager = containerProvider.Resolve<IRegionManager>();
            regionManager
                .RegisterSettingsTab<GeneralSettingsView>(containerProvider, 0)
                .RegisterSettingsTab<AppearanceSettingsView>(containerProvider, 10)
                .RegisterSettingsTab<ProcessSettingsView>(containerProvider, 20)
                .RegisterSettingsTab<NetworkSettingsView>(containerProvider, 30)
                .RegisterSettingsTab<ChangelogSettingsView>(containerProvider, 40)
                .RegisterSettingsTab<AboutSettingsView>(containerProvider, 50);
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<GeneralSettingsView>();
            containerRegistry.Register<AppearanceSettingsView>();
            containerRegistry.Register<ProcessSettingsView>();
            containerRegistry.Register<NetworkSettingsView>();
            containerRegistry.Register<ChangelogSettingsView>();
            containerRegistry.Register<AboutSettingsView>();
            ViewModelLocationProvider.Register<MainWindow, MainWindowViewModel>();
            ViewModelLocationProvider.Register<GeneralSettingsView, GeneralSettingsViewModel>();
            ViewModelLocationProvider.Register<AppearanceSettingsView, AppearanceSettingsViewModel>();
            ViewModelLocationProvider.Register<ProcessSettingsView, ProcessSettingsViewModel>();
            ViewModelLocationProvider.Register<NetworkSettingsView, NetworkSettingsViewModel>();
            ViewModelLocationProvider.Register<ChangelogSettingsView, ChangelogSettingsViewModel>();
            ViewModelLocationProvider.Register<AboutSettingsView, AboutSettingsViewModel>();
        }
    }
}
