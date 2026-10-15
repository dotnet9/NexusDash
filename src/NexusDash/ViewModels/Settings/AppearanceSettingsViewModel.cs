using NexusDash;
using CodeWF.Toolkit.EventBus;
using NexusDash.Services;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace NexusDash.ViewModels.Settings
{
    public sealed class AppearanceSettingsViewModel : SettingsPageViewModelBase
    {
        private bool _isApplyingSettingsState;
        private ThemeOptionViewModel? _selectedTheme;
        private ThemeOptionViewModel? _selectedPalette;

        public AppearanceSettingsViewModel(
            IEventBus eventBus,
            IUserPreferencesService userPreferencesService,
            IThemeResourceService themeResourceService)
            : base(eventBus, userPreferencesService)
        {
            ThemeOptions = new ObservableCollection<ThemeOptionViewModel>(
                themeResourceService.GetThemeOptions()
                    .Select(theme => new ThemeOptionViewModel(
                        theme.Key,
                        GetThemeDisplayName(theme.Key),
                        theme.AccentBrush)));
            _selectedTheme = FindTheme(ThemeKeyState);
            PaletteOptions = new ObservableCollection<ThemeOptionViewModel>(themeResourceService.GetPaletteOptions().Select(palette => new ThemeOptionViewModel(palette.Key, GetPaletteDisplayName(palette.Key), palette.AccentBrush)));
            _selectedPalette = PaletteOptions.FirstOrDefault(option => option.Key == PaletteKeyState);
        }

        public override string Header => T(NexusDashL.SettingsAppearance);
        public override int Order => 10;
        public string ThemeLabel => T(NexusDashL.ThemeMenu);
        public string PaletteLabel => T(NexusDashL.PaletteLabel);
        public ObservableCollection<ThemeOptionViewModel> PaletteOptions { get; }
        public ThemeOptionViewModel? SelectedPalette
        {
            get => _selectedPalette;
            set
            {
                if (ReferenceEquals(_selectedPalette, value)) return;
                this.RaiseAndSetIfChanged(ref _selectedPalette, value);
                if (value is not null && !_isApplyingSettingsState) EventBus.Publish(new PaletteChangeRequestedCommand(value.Key));
            }
        }
        public string DarkThemeText => T(NexusDashL.DarkTheme);
        public string LightThemeText => T(NexusDashL.LightTheme);
        public string LanguageLabel => T(NexusDashL.LanguageMenu);
        public string RememberWindowSizeText => T(NexusDashL.RememberWindowSize);
        public string SimplifiedChineseText => T(NexusDashL.SimplifiedChinese);
        public string TraditionalChineseText => T(NexusDashL.TraditionalChinese);
        public string EnglishText => T(NexusDashL.English);
        public string JapaneseText => T(NexusDashL.Japanese);
        public bool IsDarkTheme => IsDarkThemeState;
        public bool IsLightTheme => !IsDarkThemeState;
        public bool IsSimplifiedChinese => CultureNameState == "zh-CN";
        public bool IsTraditionalChinese => CultureNameState == "zh-Hant";
        public bool IsEnglish => CultureNameState == "en-US";
        public bool IsJapanese => CultureNameState == "ja-JP";
        public ObservableCollection<ThemeOptionViewModel> ThemeOptions { get; }

        public bool RememberWindowSize
        {
            get => RememberWindowSizeState;
            set
            {
                if (value == RememberWindowSizeState)
                {
                    return;
                }

                EventBus.Publish(new RememberWindowSizeChangedCommand(value));
            }
        }

        public ThemeOptionViewModel? SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (ReferenceEquals(_selectedTheme, value))
                {
                    return;
                }

                this.RaiseAndSetIfChanged(ref _selectedTheme, value, nameof(SelectedTheme));
                this.RaisePropertyChanged(nameof(ThemeMode));
                if (value is null || _isApplyingSettingsState)
                {
                    return;
                }

                EventBus.Publish(new ThemeChangeRequestedCommand(value.Key));
            }
        }

        /// <summary>标题栏三态按钮复用设置页的主题状态与保存流程。</summary>
        public CodeWF.Avalonia.Controls.Themes.StudioThemeMode ThemeMode
        {
            get => ThemeResourceService.ResolveMode(SelectedTheme?.Key);
            set => SelectedTheme = ThemeOptions.Single(option => option.Key == (value switch
            {
                CodeWF.Avalonia.Controls.Themes.StudioThemeMode.Light => ThemeResourceService.LightThemeKey,
                CodeWF.Avalonia.Controls.Themes.StudioThemeMode.Dark => ThemeResourceService.DarkThemeKey,
                _ => ThemeResourceService.SystemThemeKey,
            }));
        }

        public void SetDarkTheme()
        {
            EventBus.Publish(new ThemeChangeRequestedCommand(ThemeResourceService.DarkThemeKey));
        }

        public void SetLightTheme()
        {
            EventBus.Publish(new ThemeChangeRequestedCommand(ThemeResourceService.LightThemeKey));
        }

        public void SelectSimplifiedChinese()
        {
            EventBus.Publish(new LanguageChangeRequestedCommand("zh-CN"));
        }

        public void SelectTraditionalChinese()
        {
            EventBus.Publish(new LanguageChangeRequestedCommand("zh-Hant"));
        }

        public void SelectEnglish()
        {
            EventBus.Publish(new LanguageChangeRequestedCommand("en-US"));
        }

        public void SelectJapanese()
        {
            EventBus.Publish(new LanguageChangeRequestedCommand("ja-JP"));
        }

        protected override void RaiseLocalizedProperties()
        {
            this.RaisePropertyChanged(nameof(Header));
            this.RaisePropertyChanged(nameof(ThemeLabel));
            this.RaisePropertyChanged(nameof(PaletteLabel));
            foreach (var option in PaletteOptions) option.DisplayName = GetPaletteDisplayName(option.Key);
            this.RaisePropertyChanged(nameof(DarkThemeText));
            this.RaisePropertyChanged(nameof(LightThemeText));
            RefreshThemeOptionLabels();
            SyncSelectedThemeFromState();
            this.RaisePropertyChanged(nameof(LanguageLabel));
            this.RaisePropertyChanged(nameof(RememberWindowSizeText));
            this.RaisePropertyChanged(nameof(RememberWindowSize));
            this.RaisePropertyChanged(nameof(SimplifiedChineseText));
            this.RaisePropertyChanged(nameof(TraditionalChineseText));
            this.RaisePropertyChanged(nameof(EnglishText));
            this.RaisePropertyChanged(nameof(JapaneseText));
            this.RaisePropertyChanged(nameof(IsDarkTheme));
            this.RaisePropertyChanged(nameof(IsLightTheme));
            this.RaisePropertyChanged(nameof(IsSimplifiedChinese));
            this.RaisePropertyChanged(nameof(IsTraditionalChinese));
            this.RaisePropertyChanged(nameof(IsEnglish));
            this.RaisePropertyChanged(nameof(IsJapanese));
        }

        private void RefreshThemeOptionLabels()
        {
            foreach (var option in ThemeOptions)
            {
                option.DisplayName = GetThemeDisplayName(option.Key);
            }
        }

        private void SyncSelectedThemeFromState()
        {
            _isApplyingSettingsState = true;
            try
            {
                SelectedTheme = FindTheme(ThemeKeyState);
                SelectedPalette = PaletteOptions.FirstOrDefault(option => option.Key == PaletteKeyState);
            }
            finally
            {
                _isApplyingSettingsState = false;
            }
        }

        private ThemeOptionViewModel? FindTheme(string? key)
        {
            return ThemeOptions.FirstOrDefault(theme =>
                       string.Equals(theme.Key, key, StringComparison.OrdinalIgnoreCase))
                   ?? ThemeOptions.FirstOrDefault();
        }

        private static string GetPaletteDisplayName(string key) => T(key switch
        {
            "dotnet" => NexusDashL.PaletteDotNet,
            "aurora" => NexusDashL.PaletteAurora,
            "ember" => NexusDashL.PaletteEmber,
            "default" => NexusDashL.PaletteDefault,
            "aquatic" => NexusDashL.ThemeAquatic,
            "desert" => NexusDashL.ThemeDesert,
            "dusk" => NexusDashL.ThemeDusk,
            "night-sky" => NexusDashL.ThemeNightSky,
            _ => NexusDashL.PaletteDotNet
        });

        private static string GetThemeDisplayName(string key)
        {
            return key switch
            {
                ThemeResourceService.SystemThemeKey => T(NexusDashL.ThemeSystem),
                ThemeResourceService.LightThemeKey => T(NexusDashL.LightTheme),
                ThemeResourceService.DarkThemeKey => T(NexusDashL.DarkTheme),
                ThemeResourceService.AquaticThemeKey => T(NexusDashL.ThemeAquatic),
                ThemeResourceService.DesertThemeKey => T(NexusDashL.ThemeDesert),
                ThemeResourceService.DuskThemeKey => T(NexusDashL.ThemeDusk),
                ThemeResourceService.NightSkyThemeKey => T(NexusDashL.ThemeNightSky),
                _ => key
            };
        }
    }
}
