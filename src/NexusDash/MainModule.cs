using NexusDash.ViewModels;
using NexusDash.ViewModels.Settings;
using NexusDash.Views.Settings;
using NexusDash.Regions;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;

namespace NexusDash
{
    public sealed class MainModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var regionManager = containerProvider.Resolve<IRegionManager>();
            regionManager
                .RegisterSettingsTab<AppearanceSettingsView>(containerProvider, 10)
                .RegisterSettingsTab<ChangelogSettingsView>(containerProvider, 20)
                .RegisterSettingsTab<AboutSettingsView>(containerProvider, 30);
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<AppearanceSettingsView>();
            containerRegistry.Register<ChangelogSettingsView>();
            containerRegistry.Register<AboutSettingsView>();
        }
    }
}
