using Avalonia.Media;

namespace NexusDash.Models
{
    public sealed record ThemeOption(string Key, string Name, IBrush AccentBrush, bool IsDark);
}
