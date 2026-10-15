using Avalonia;
using CodeWF.Avalonia.Controls.Themes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using CodeWF.Toolkit.EventBus;
using CodeWF.Toolkit.Logging;
using DryIoc;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
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
using System.Globalization;
using System.IO;
using System.Linq;

namespace NexusDash
{
    public partial class App : PrismApplication
    {
        public static StudioThemeController? ThemeController { get; private set; }
        private IUserPreferencesService? _preferences;
        protected virtual IUserPreferencesService CreatePreferencesService() => new UserPreferencesService();

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            _preferences = CreatePreferencesService();
            var preferences = _preferences.Load();
            var startupCulture = ResolveStartupCulture(preferences.CultureName);
            CultureInfo.CurrentCulture = startupCulture;
            CultureInfo.CurrentUICulture = startupCulture;
            CultureInfo.DefaultThreadCurrentCulture = startupCulture;
            CultureInfo.DefaultThreadCurrentUICulture = startupCulture;

            var resourceFolder = Path.Combine(AppContext.BaseDirectory, "I18n");
            var bundleResources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "I18n"));
            if (!Directory.Exists(resourceFolder) && OperatingSystem.IsMacOS() && Directory.Exists(bundleResources))
                resourceFolder = bundleResources;
            var langPlugin = new JsonLangPlugin
            {
                ResourceFolder = resourceFolder
            };
            I18nManager.Instance.Register(langPlugin, startupCulture, out _);
            I18nManager.Instance.CultureChanged += OnCultureChanged;
            ApplyControlCulture();
            ThemeController = new StudioThemeController(this)
            {
                Palette = ThemeResourceService.ResolvePalette(ThemeResourceService.ResolvePreferencePaletteKey(preferences.PaletteKey, preferences.ThemeKey)),
                Mode = ThemeResourceService.ResolveMode(ThemeResourceService.ResolvePreferenceThemeKey(preferences.ThemeKey, preferences.IsDarkTheme))
            };
            Logger.Info("NexusDash application initialized.", I18nManager.Instance.GetResource(NexusDashL.LogApplicationStarted));
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
            containerRegistry.RegisterInstance<IUserPreferencesService>(_preferences!);
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

        private static void OnCultureChanged(object? sender, EventArgs args)
        {
            if (Dispatcher.UIThread.CheckAccess()) ApplyControlCulture();
            else Dispatcher.UIThread.Post(ApplyControlCulture);
        }

        /// <summary>同步已加载主题的内建语言，保留应用和局部资源的同键覆盖。</summary>
        private static void ApplyControlCulture()
        {
            if (Current is not { } app || I18nManager.Instance.Culture is not { } culture) return;
            var name = NormalizeCulture(culture.Name);
            var semiCulture = CultureInfo.GetCultureInfo(name == "zh-Hant" ? "zh-TW" : name);
            // 当前 Ursa 包缺少日语和繁体资源，分别回退英文和简体中文。
            var ursaCulture = CultureInfo.GetCultureInfo(name switch { "ja-JP" => "en-US", "zh-Hant" => "zh-CN", _ => name });
            foreach (var theme in app.Styles.OfType<Semi.Avalonia.SemiTheme>()) theme.Locale = semiCulture;
            foreach (var theme in app.Styles.OfType<Ursa.Themes.Semi.UrsaSemiTheme>()) theme.Locale = ursaCulture;
        }
    }
}
