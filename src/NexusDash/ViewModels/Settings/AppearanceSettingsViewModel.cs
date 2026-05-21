using NexusDash;
using NexusDash.ViewModels;
using ReactiveUI;
using System.Collections.ObjectModel;

namespace NexusDash.ViewModels.Settings
{
    public sealed class AppearanceSettingsViewModel(MainWindowViewModel mainViewModel) : SettingsPageViewModelBase(mainViewModel)
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
        public ObservableCollection<LanguageOption> Languages => MainViewModel.Languages;
        public bool IsDarkTheme => MainViewModel.IsDarkTheme;
        public bool IsLightTheme => MainViewModel.IsLightTheme;
        public bool IsSimplifiedChinese => MainViewModel.IsSimplifiedChinese;
        public bool IsTraditionalChinese => MainViewModel.IsTraditionalChinese;
        public bool IsEnglish => MainViewModel.IsEnglish;
        public bool IsJapanese => MainViewModel.IsJapanese;

        public void SetDarkTheme()
        {
            MainViewModel.SetDarkTheme();
        }

        public void SetLightTheme()
        {
            MainViewModel.SetLightTheme();
        }

        public void SelectSimplifiedChinese()
        {
            MainViewModel.SelectSimplifiedChinese();
        }

        public void SelectTraditionalChinese()
        {
            MainViewModel.SelectTraditionalChinese();
        }

        public void SelectEnglish()
        {
            MainViewModel.SelectEnglish();
        }

        public void SelectJapanese()
        {
            MainViewModel.SelectJapanese();
        }

        protected override bool ShouldRefreshFromMainPropertyChanged(string? propertyName)
        {
            return base.ShouldRefreshFromMainPropertyChanged(propertyName) ||
                   propertyName == nameof(MainWindowViewModel.IsDarkTheme) ||
                   propertyName == nameof(MainWindowViewModel.IsLightTheme) ||
                   propertyName == nameof(MainWindowViewModel.IsSimplifiedChinese) ||
                   propertyName == nameof(MainWindowViewModel.IsTraditionalChinese) ||
                   propertyName == nameof(MainWindowViewModel.IsEnglish) ||
                   propertyName == nameof(MainWindowViewModel.IsJapanese);
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
