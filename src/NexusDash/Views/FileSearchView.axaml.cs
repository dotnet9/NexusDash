using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using CodeWF.Log.Core;
using NexusDash.ViewModels;
using System.Diagnostics;
using System.IO;

namespace NexusDash.Views
{
    public partial class FileSearchView : UserControl
    {
        public FileSearchView()
        {
            AvaloniaXamlLoader.Load(this);
            ConfigureResultsGrid();
        }

        private void ConfigureResultsGrid()
        {
            var resultsGrid = ResultsGrid ?? this.FindControl<DataGrid>("ResultsGrid");
            resultsGrid?.AddHandler(
                PointerPressedEvent,
                ResultsGrid_PointerPressed,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
        }

        private void ResultsGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not DataGrid dataGrid ||
                !e.GetCurrentPoint(dataGrid).Properties.IsRightButtonPressed ||
                e.Source is not Visual source)
            {
                return;
            }

            var row = source.FindAncestorOfType<DataGridRow>(includeSelf: true);
            if (row?.DataContext is not FileSearchResultViewModel result)
            {
                return;
            }

            dataGrid.SelectedItem = result;
            e.Handled = true;
            ShowResultMenu(dataGrid, result);
        }

        private void ShowResultMenu(DataGrid dataGrid, FileSearchResultViewModel result)
        {
            var viewModel = DataContext as FileSearchViewModel;
            var menu = new AtomUI.Desktop.Controls.MenuFlyout
            {
                Placement = PlacementMode.Pointer,
                IsMotionEnabled = true
            };

            var openContainingDirectoryItem = new AtomUI.Desktop.Controls.MenuItem
            {
                Header = viewModel?.OpenContainingDirectoryText ?? "Open file location",
                IsEnabled = !string.IsNullOrWhiteSpace(result.FullPath)
            };
            openContainingDirectoryItem.Click += (_, _) => OpenContainingDirectory(result);
            menu.Items.Add(openContainingDirectoryItem);

            menu.ShowAt(dataGrid);
        }

        private static void OpenContainingDirectory(FileSearchResultViewModel result)
        {
            var path = result.FullPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var arguments = CreateExplorerArguments(result);
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return;
            }

            try
            {
                Logger.Info(
                    $"Open containing directory: {path}",
                    $"打开文件所在目录：{path}",
                    log2Console: false);
                Process.Start(new ProcessStartInfo("explorer.exe", arguments)
                {
                    UseShellExecute = true
                });
            }
            catch (System.Exception exception)
            {
                Logger.Error(
                    $"Open containing directory failed: {path}",
                    exception,
                    $"打开文件所在目录失败：{path}",
                    log2Console: false);
            }
        }

        private static string CreateExplorerArguments(FileSearchResultViewModel result)
        {
            var path = result.FullPath;
            if (File.Exists(path) || Directory.Exists(path))
            {
                return $"/select,\"{path}\"";
            }

            return Directory.Exists(result.DirectoryPath)
                ? $"\"{result.DirectoryPath}\""
                : "";
        }
    }
}
