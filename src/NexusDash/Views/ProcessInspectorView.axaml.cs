using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CodeWF.AvaloniaControls;
using CodeWF.AvaloniaControls.ProDataGrid;

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
