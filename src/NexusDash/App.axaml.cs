using AtomUI;
using AtomUI.Controls;
using AtomUI.Desktop.Controls;
using AtomUI.Theme;
using AtomUI.Theme.Language;
using Avalonia;
using Avalonia.Markup.Xaml;
using CodeWF.EventBus;
using DryIoc;
using Lang.Avalonia;
using Lang.Avalonia.Json;
using NexusDash.Regions;
using NexusDash.ViewModels;
using NexusDash.ViewModels.Settings;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using System;
using System.Globalization;
using System.IO;

namespace NexusDash
{
    public partial class App : PrismApplication
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            var langPlugin = new JsonLangPlugin
            {
                ResourceFolder = Path.Combine(AppContext.BaseDirectory, "I18n")
            };
            I18nManager.Instance.Register(langPlugin, new CultureInfo("zh-CN"), out _);

            this.UseAtomUI(builder =>
            {
                builder.WithDefaultLanguageVariant(LanguageVariant.zh_CN);
                builder.WithDefaultTheme(IThemeManager.DEFAULT_THEME_ID);
                builder.UseAlibabaSansFont();
                builder.UseDesktopControls();
                builder.UseDesktopDataGrid();
            });

            this.SetDarkThemeMode(true);
            base.Initialize();
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<MainModule>();
            base.ConfigureModuleCatalog(moduleCatalog);
        }

        protected override AvaloniaObject CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void ConfigureRegionAdapterMappings(RegionAdapterMappings regionAdapterMappings)
        {
            base.ConfigureRegionAdapterMappings(regionAdapterMappings);
            regionAdapterMappings.RegisterMapping(
                typeof(AtomUI.Desktop.Controls.TabControl),
                Container.Resolve<SettingsTabControlRegionAdapter>());
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterInstance<IEventBus>(EventBus.Default);
            containerRegistry.RegisterSingleton<SettingsTabControlRegionAdapter>();
            containerRegistry.RegisterSingleton<ProcessListViewModel>();
            containerRegistry.RegisterSingleton<MainWindowViewModel>();
            containerRegistry.Register<GeneralSettingsViewModel>();
            containerRegistry.Register<AppearanceSettingsViewModel>();
            containerRegistry.Register<ProcessSettingsViewModel>();
            containerRegistry.Register<NetworkSettingsViewModel>();
            containerRegistry.Register<ChangelogSettingsViewModel>();
            containerRegistry.Register<AboutSettingsViewModel>();
            containerRegistry.Register<MainWindow>();
            containerRegistry.Register<SettingsWindow>();
        }
    }
}
