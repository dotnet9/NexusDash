using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using CodeWF.EventBus;
using CodeWF.Log.Core;
using DryIoc;
using Lang.Avalonia;
using Lang.Avalonia.Json;
using NexusDash.Services;
using NexusDash.ViewModels;
using NexusDash.ViewModels.Settings;
using NexusDash.Views;
using NexusDash.Views.Settings;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
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
            var preferences = new UserPreferencesService().Load();
            var startupCulture = ResolveStartupCulture(preferences.CultureName);
            CultureInfo.CurrentCulture = startupCulture;
            CultureInfo.CurrentUICulture = startupCulture;
            CultureInfo.DefaultThreadCurrentCulture = startupCulture;
            CultureInfo.DefaultThreadCurrentUICulture = startupCulture;

            var langPlugin = new JsonLangPlugin
            {
                ResourceFolder = Path.Combine(AppContext.BaseDirectory, "I18n")
            };
            I18nManager.Instance.Register(langPlugin, startupCulture, out _);
            ApplyThirdPartyCulture(startupCulture.Name);
            RequestedThemeVariant = ThemeResourceService.ResolveThemeVariant(
                ThemeResourceService.ResolvePreferenceThemeKey(preferences.ThemeKey, preferences.IsDarkTheme));
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
            return Container.Resolve<MainWindow>();
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
            containerRegistry.RegisterSingleton<HardwareInfoService>();
            containerRegistry.RegisterSingleton<IProcessSnapshotExportService, ProcessSnapshotExportService>();
            containerRegistry.RegisterSingleton<ProcessListViewModel>();
            containerRegistry.RegisterSingleton<FileSearchViewModel>();
            containerRegistry.RegisterSingleton<HardwareInfoViewModel>();
            containerRegistry.RegisterSingleton<SettingsViewModel>();
            containerRegistry.RegisterSingleton<MainWindowViewModel>();
            containerRegistry.Register<AppearanceSettingsViewModel>();
            containerRegistry.Register<ChangelogSettingsViewModel>();
            containerRegistry.Register<AboutSettingsViewModel>();
            containerRegistry.Register<MainWindow>();
            containerRegistry.Register<SettingsView>();

            ViewModelLocationProvider.Register<MainWindow, MainWindowViewModel>();
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
            Logger.BatchProcessSize = 40;
            Logger.LogUIDuration = 2000;
            Logger.MaxUIDisplayCount = 200;
            Logger.MaxLogFileSizeMB = 20;
            Logger.TimeFormat = "HH:mm:ss";
            Logger.EnableConsoleOutput = false;
        }

        internal static CultureInfo ResolveStartupCulture(string? configuredCultureName)
        {
            var cultureName = string.IsNullOrWhiteSpace(configuredCultureName)
                ? CultureInfo.CurrentUICulture.Name
                : configuredCultureName;
            return CultureInfo.GetCultureInfo(NormalizeCulture(cultureName));
        }

        internal static string NormalizeCulture(string? cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName))
            {
                return "en-US";
            }

            if (cultureName.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
                cultureName.Equals("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                cultureName.Equals("zh-HK", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-Hant";
            }

            if (cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-CN";
            }

            if (cultureName.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            {
                return "ja-JP";
            }

            if (cultureName.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                return "en-US";
            }

            return "en-US";
        }

        internal static void ApplyThirdPartyCulture(string cultureName)
        {
            if (Current is not { } app)
            {
                return;
            }

            var normalizedCultureName = NormalizeCulture(cultureName);
            if (SemiCultures.TryGetValue(normalizedCultureName, out var semiCulture))
            {
                MergeCultureResources(app.Resources, semiCulture);
            }

            if (UrsaCultures.TryGetValue(normalizedCultureName, out var ursaCulture))
            {
                MergeCultureResources(app.Resources, ursaCulture);
            }
        }

        private static readonly IReadOnlyDictionary<string, ResourceDictionary> SemiCultures =
            new Dictionary<string, ResourceDictionary>(StringComparer.OrdinalIgnoreCase)
            {
                ["en-US"] = new Semi.Avalonia.Locale.en_us(),
                ["ja-JP"] = new Semi.Avalonia.Locale.ja_jp(),
                ["zh-CN"] = new Semi.Avalonia.Locale.zh_cn(),
                ["zh-Hant"] = new Semi.Avalonia.Locale.zh_cn()
            };

        private static readonly IReadOnlyDictionary<string, ResourceDictionary> UrsaCultures =
            new Dictionary<string, ResourceDictionary>(StringComparer.OrdinalIgnoreCase)
            {
                ["en-US"] = new Ursa.Themes.Semi.Locale.en_us(),
                ["ja-JP"] = new Ursa.Themes.Semi.Locale.en_us(),
                ["zh-CN"] = new Ursa.Themes.Semi.Locale.zh_cn(),
                ["zh-Hant"] = new Ursa.Themes.Semi.Locale.zh_cn()
            };

        private static void MergeCultureResources(IResourceDictionary appResources, ResourceDictionary cultureResources)
        {
            foreach (var item in cultureResources)
            {
                if (appResources.ContainsKey(item.Key))
                {
                    appResources.Remove(item.Key);
                }

                appResources.Add(item);
            }
        }
    }
}
