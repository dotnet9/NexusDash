using CodeWF.EventBus;

namespace NexusDash.ViewModels.Settings
{
    public sealed class SettingsStateChangedCommand(bool isDarkTheme, string cultureName) : Command
    {
        public bool IsDarkTheme { get; } = isDarkTheme;
        public string CultureName { get; } = cultureName;
    }

    public sealed class ThemeChangeRequestedCommand(bool isDarkTheme) : Command
    {
        public bool IsDarkTheme { get; } = isDarkTheme;
    }

    public sealed class LanguageChangeRequestedCommand(string cultureName) : Command
    {
        public string CultureName { get; } = cultureName;
    }
}
