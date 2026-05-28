using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using NexusDash.Models;
using NexusDash.ViewModels;
using System.Linq;
using System.Threading.Tasks;

namespace NexusDash.Views
{
    public partial class ProcessInspectorView : UserControl
    {
        public ProcessInspectorView()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void NetworkConnectionsGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not DataGrid dataGrid ||
                !e.GetCurrentPoint(dataGrid).Properties.IsRightButtonPressed ||
                e.Source is not Visual source)
            {
                return;
            }

            var row = source.FindAncestorOfType<DataGridRow>(includeSelf: true);
            if (row?.DataContext is not ProcessNetworkConnection connection)
            {
                return;
            }

            e.Handled = true;
            ShowNetworkConnectionMenu(dataGrid, connection);
        }

        private void ShowNetworkConnectionMenu(DataGrid dataGrid, ProcessNetworkConnection connection)
        {
            var viewModel = DataContext as ProcessInspectorViewModel;
            var menu = new AtomUI.Desktop.Controls.MenuFlyout
            {
                Placement = PlacementMode.Pointer,
                IsMotionEnabled = true
            };

            AddCopyMenuItem(
                menu,
                viewModel?.CopyLocalEndpointText ?? "Copy local endpoint",
                connection.LocalEndpointText);
            AddCopyMenuItem(
                menu,
                viewModel?.CopyRemoteEndpointText ?? "Copy remote endpoint",
                connection.RemoteEndpointText);
            AddCopyMenuItem(
                menu,
                viewModel?.CopyConnectionInfoText ?? "Copy connection row",
                FormatConnectionInfo(connection));

            menu.ShowAt(dataGrid);
        }

        private void AddCopyMenuItem(AtomUI.Desktop.Controls.MenuFlyout menu, string header, string text)
        {
            var item = new AtomUI.Desktop.Controls.MenuItem
            {
                Header = header
            };
            item.Click += async (_, _) => await CopyTextAsync(text);
            menu.Items.Add(item);
        }

        private async Task CopyTextAsync(string text)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(text);
            }
        }

        private static string FormatConnectionInfo(ProcessNetworkConnection connection)
        {
            return string.Join(
                "\t",
                connection.Protocol,
                connection.LocalEndpointText,
                connection.RemoteEndpointText,
                connection.State,
                connection.TimestampText);
        }
    }
}
