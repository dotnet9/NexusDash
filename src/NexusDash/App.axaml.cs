using AtomUI;
using AtomUI.Controls;
using AtomUI.Desktop.Controls;
using AtomUI.Theme;
using AtomUI.Theme.Language;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lang.Avalonia;
using Lang.Avalonia.Json;
using System;
using System.Globalization;
using System.IO;

namespace NexusDash
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            base.Initialize();
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
            });

            this.SetDarkThemeMode(true);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
