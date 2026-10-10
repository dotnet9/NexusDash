using CodeWF.Avalonia.DataGrid;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CodeWF.Avalonia.Controls;

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
