using NexusDash;
using CodeWF.EventBus;
using NexusDash.Services;
using ReactiveUI;

namespace NexusDash.ViewModels.Settings
{
    public sealed class AppearanceSettingsViewModel(
        IEventBus eventBus,
        IUserPreferencesService userPreferencesService)
        : SettingsPageViewModelBase(eventBus, userPreferencesService)
    {
        public override string Header => T(NexusDashL.SettingsAppearance);
        public override int Order => 10;
        public string ThemeLabel => T(NexusDashL.ThemeMenu);
        public string DarkThemeText => T(NexusDashL.DarkTheme);
        public string LightThemeText => T(NexusDashL.LightTheme);
        public string LanguageLabel => T(NexusDashL.LanguageMenu);
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

        public void SetDarkTheme()
        {
            EventBus.Publish(new ThemeChangeRequestedCommand(isDarkTheme: true));
        }

        public void SetLightTheme()
        {
            EventBus.Publish(new ThemeChangeRequestedCommand(isDarkTheme: false));
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
            this.RaisePropertyChanged(nameof(DarkThemeText));
            this.RaisePropertyChanged(nameof(LightThemeText));
            this.RaisePropertyChanged(nameof(LanguageLabel));
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
    }
}
