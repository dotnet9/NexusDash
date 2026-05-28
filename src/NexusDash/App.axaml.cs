using AtomUI;
using AtomUI.Controls;
using AtomUI.Desktop.Controls;
using AtomUI.Theme;
using AtomUI.Theme.Language;
using Avalonia;
using Avalonia.Markup.Xaml;
using CodeWF.EventBus;
using CodeWF.Log.Core;
using DryIoc;
using Lang.Avalonia;
using Lang.Avalonia.Json;
using NexusDash.Regions;
using NexusDash.Services;
using NexusDash.ViewModels;
using NexusDash.ViewModels.Settings;
using NexusDash.Views.Settings;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Mvvm;
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
            ConfigureOperationLogger();
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
            Logger.Info("NexusDash application initialized.", "NexusDash 已启动。", log2Console: false);
            base.Initialize();
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<MainModule>();
            base.ConfigureModuleCatalog(moduleCatalog);
        }

        protected override AvaloniaObject CreateShell()
        {
            Container.Resolve<ISettingsWindowService>();
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
            containerRegistry.RegisterSingleton<IUserPreferencesService, UserPreferencesService>();
            containerRegistry.RegisterSingleton<IProcessCommandRunner, ProcessCommandRunner>();
            containerRegistry.RegisterSingleton<IThemeResourceService, ThemeResourceService>();
            containerRegistry.RegisterSingleton<SystemMonitorService>();
            containerRegistry.RegisterSingleton<ProcessTelemetryService>();
            containerRegistry.RegisterSingleton<ProcessNetworkConnectionService>();
            containerRegistry.RegisterSingleton<FileSearchService>();
            containerRegistry.RegisterSingleton<ISettingsWindowService, SettingsWindowService>();
            containerRegistry.RegisterSingleton<SettingsTabControlRegionAdapter>();
            containerRegistry.RegisterSingleton<ProcessListViewModel>();
            containerRegistry.RegisterSingleton<FileSearchViewModel>();
            containerRegistry.RegisterSingleton<MainWindowViewModel>();
            containerRegistry.Register<AppearanceSettingsViewModel>();
            containerRegistry.Register<ChangelogSettingsViewModel>();
            containerRegistry.Register<AboutSettingsViewModel>();
            containerRegistry.Register<SettingsWindowViewModel>();
            containerRegistry.Register<MainWindow>();
            containerRegistry.Register<SettingsWindow>();

            ViewModelLocationProvider.Register<MainWindow, MainWindowViewModel>();
            ViewModelLocationProvider.Register<SettingsWindow, SettingsWindowViewModel>();
            ViewModelLocationProvider.Register<AppearanceSettingsView, AppearanceSettingsViewModel>();
            ViewModelLocationProvider.Register<ChangelogSettingsView, ChangelogSettingsViewModel>();
            ViewModelLocationProvider.Register<AboutSettingsView, AboutSettingsViewModel>();
        }

        private static void ConfigureOperationLogger()
        {
            Logger.Level = LogType.Debug;
            Logger.LogDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NexusDash");
            Logger.BatchProcessSize = 80;
            Logger.LogUIDuration = 80;
            Logger.MaxUIDisplayCount = 1200;
            Logger.MaxLogFileSizeMB = 20;
            Logger.TimeFormat = "HH:mm:ss";
            Logger.EnableConsoleOutput = false;
        }
    }
}
