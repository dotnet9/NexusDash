using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace NexusDash.Controls.Themes;

public sealed class NexusDashControlsThemes : Styles
{
    public NexusDashControlsThemes()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
