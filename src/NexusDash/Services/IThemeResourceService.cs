using NexusDash.Models;
using System.Collections.Generic;

namespace NexusDash.Services
{
    public interface IThemeResourceService : System.IDisposable
    {
        IReadOnlyList<ThemeOption> GetThemeOptions();
        ThemeOption GetThemeOption(string? themeKey);
        IReadOnlyList<ThemeOption> GetPaletteOptions();
        void ApplyPalette(string paletteKey);
        bool IsEffectiveDark { get; }
        void Apply(string themeKey);
        void Apply(bool isDarkTheme);
    }
}
