using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using NexusDash.Models;
using NexusDash.ViewModels;
using System.Threading.Tasks;

namespace NexusDash.Views.Behaviors
{
    public sealed class NetworkConnectionMenuBehavior : Behavior<DataGrid>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject is not null)
            {
                AssociatedObject.PointerPressed += HandlePointerPressed;
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject is not null)
            {
                AssociatedObject.PointerPressed -= HandlePointerPressed;
            }

            base.OnDetaching();
        }

        private void HandlePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (AssociatedObject is not { } dataGrid ||
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

        private static void ShowNetworkConnectionMenu(DataGrid dataGrid, ProcessNetworkConnection connection)
        {
            var viewModel = dataGrid.DataContext as ProcessInspectorViewModel;
            var menu = new MenuFlyout
            {
                Placement = PlacementMode.Pointer
            };

            AddCopyMenuItem(
                dataGrid,
                menu,
                viewModel?.CopyLocalEndpointText ?? "Copy local endpoint",
                connection.LocalEndpointText);
            AddCopyMenuItem(
                dataGrid,
                menu,
                viewModel?.CopyRemoteEndpointText ?? "Copy remote endpoint",
                connection.RemoteEndpointText);
            AddCopyMenuItem(
                dataGrid,
                menu,
                viewModel?.CopyConnectionInfoText ?? "Copy connection row",
                FormatConnectionInfo(connection));

            menu.ShowAt(dataGrid);
        }

        private static void AddCopyMenuItem(DataGrid dataGrid, MenuFlyout menu, string header, string text)
        {
            var item = new MenuItem
            {
                Header = header
            };
            item.Click += async (_, _) => await CopyTextAsync(dataGrid, text);
            menu.Items.Add(item);
        }

        private static async Task CopyTextAsync(DataGrid dataGrid, string text)
        {
            var clipboard = TopLevel.GetTopLevel(dataGrid)?.Clipboard;
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
