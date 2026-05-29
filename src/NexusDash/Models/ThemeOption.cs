using Avalonia.Styling;

namespace NexusDash.Models
{
    public sealed record ThemeOption(
        string Key,
        string Name,
        ThemeVariant ThemeVariant,
        string AccentColor,
        bool IsDark);
}
