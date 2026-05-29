using NexusDash.Views.Settings;
using Prism.Ioc;
using Prism.Modularity;

namespace NexusDash
{
    public sealed class MainModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<AppearanceSettingsView>();
            containerRegistry.Register<ChangelogSettingsView>();
            containerRegistry.Register<AboutSettingsView>();
        }
    }
}
