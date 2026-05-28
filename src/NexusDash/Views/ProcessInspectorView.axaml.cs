using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CodeWF.AvaloniaControls;

namespace NexusDash.Views
{
    public partial class ProcessInspectorView : UserControl
    {
        public ProcessInspectorView()
        {
            AvaloniaXamlLoader.Load(this);
            NetworkConnectionsGrid?.ApplyPerformancePreset();
        }
    }
}
