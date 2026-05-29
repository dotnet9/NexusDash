using CodeWF.EventBus;

namespace NexusDash.ViewModels.Settings
{
    public sealed class SettingsStateChangedCommand(
        string themeKey,
        bool isDarkTheme,
        bool rememberWindowSize,
        string cultureName) : Command
    {
        public string ThemeKey { get; } = themeKey;
        public bool IsDarkTheme { get; } = isDarkTheme;
        public bool RememberWindowSize { get; } = rememberWindowSize;
        public string CultureName { get; } = cultureName;
    }

    public sealed class ThemeChangeRequestedCommand(string themeKey) : Command
    {
        public string ThemeKey { get; } = themeKey;
    }

    public sealed class LanguageChangeRequestedCommand(string cultureName) : Command
    {
        public string CultureName { get; } = cultureName;
    }
}
