using NexusDash.Models;
using System.Collections.Generic;

namespace NexusDash.Services
{
    public interface IThemeResourceService
    {
        IReadOnlyList<ThemeOption> GetThemeOptions();
        ThemeOption GetThemeOption(string? themeKey);
        void Apply(string themeKey);
        void Apply(bool isDarkTheme);
    }
}
