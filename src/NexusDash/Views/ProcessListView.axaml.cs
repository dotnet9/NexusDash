using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using NexusDash.ViewModels;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace NexusDash.Views
{
    public partial class ProcessListView : UserControl
    {
        private ProcessListViewModel? _viewModel;

        public ProcessListView()
        {
            AvaloniaXamlLoader.Load(this);
            ConfigureProcessGrid();
            DataContextChanged += (_, _) => AttachViewModel(DataContext as ProcessListViewModel);
        }

        private void ConfigureProcessGrid()
        {
            var processGrid = GetProcessGrid();
            if (processGrid is null)
            {
                return;
            }

            processGrid.AddHandler(
                PointerPressedEvent,
                ProcessGrid_PointerPressed,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
        }

        private void AttachViewModel(ProcessListViewModel? viewModel)
        {
            if (ReferenceEquals(_viewModel, viewModel))
            {
                return;
            }

            if (_viewModel is not null)
            {
                _viewModel.ProcessColumns.CollectionChanged -= HandleProcessColumnsChanged;
                foreach (var option in _viewModel.ProcessColumns)
                {
                    option.PropertyChanged -= HandleColumnOptionChanged;
                }
            }

            _viewModel = viewModel;

            if (_viewModel is not null)
            {
                _viewModel.ProcessColumns.CollectionChanged += HandleProcessColumnsChanged;
                foreach (var option in _viewModel.ProcessColumns)
                {
                    option.PropertyChanged += HandleColumnOptionChanged;
                }
            }

            ApplyColumnVisibility();
        }

        private void ProcessGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_viewModel is null || sender is not DataGrid dataGrid)
            {
                return;
            }

            var selectedItems = dataGrid.SelectedItems.OfType<ProcessRowViewModel>().ToArray();
            foreach (var groupHeader in selectedItems.Where(static row => row.IsGroupHeader))
            {
                dataGrid.SelectedItems.Remove(groupHeader);
            }

            var selectedRows = selectedItems
                .Where(static row => !row.IsGroupHeader)
                .ToArray();
            if (selectedRows.Length == 0 && _viewModel.IsSelectedProcessStillVisible())
            {
                return;
            }

            _viewModel.SetSelectedProcesses(selectedRows);
        }

        private void ProcessGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_viewModel is null ||
                sender is not DataGrid dataGrid ||
                !e.GetCurrentPoint(dataGrid).Properties.IsRightButtonPressed ||
                e.Source is not Visual source)
            {
                return;
            }

            if (IsColumnHeaderSource(source, dataGrid))
            {
                e.Handled = true;
                ShowColumnMenu(dataGrid);
                return;
            }

            var row = source.FindAncestorOfType<DataGridRow>(includeSelf: true);
            if (row?.DataContext is not ProcessRowViewModel process)
            {
                return;
            }

            if (!IsCurrentProcessGridItem(process))
            {
                e.Handled = true;
                return;
            }

            if (process.IsGroupHeader)
            {
                dataGrid.SelectedItems.Clear();
                _viewModel.SetSelectedProcesses([]);
                return;
            }

            if (!TrySelectProcessForContextMenu(dataGrid, process))
            {
                e.Handled = true;
                return;
            }

            _viewModel.SetSelectedProcesses(dataGrid.SelectedItems
                .OfType<ProcessRowViewModel>()
                .Where(IsCurrentProcessGridItem));
            e.Handled = true;
            ShowProcessMenu(dataGrid);
        }

        private bool TrySelectProcessForContextMenu(DataGrid dataGrid, ProcessRowViewModel process)
        {
            if (dataGrid.SelectedItems.Contains(process))
            {
                return true;
            }

            dataGrid.SelectedItems.Clear();
            try
            {
                dataGrid.SelectedItems.Add(process);
                return true;
            }
            catch (ArgumentException)
            {
                _viewModel?.SetSelectedProcesses([]);
                return false;
            }
        }

        private bool IsCurrentProcessGridItem(ProcessRowViewModel process)
        {
            return _viewModel?.VisibleProcesses.Contains(process) == true;
        }

        private void ShowProcessMenu(DataGrid dataGrid)
        {
            if (_viewModel is null)
            {
                return;
            }

            var menu = new AtomUI.Desktop.Controls.MenuFlyout
            {
                Placement = PlacementMode.Pointer,
                IsMotionEnabled = true
            };

            var endProcessItem = new AtomUI.Desktop.Controls.MenuItem
            {
                Header = _viewModel.EndProcessText,
                IsEnabled = _viewModel.HasSelectedProcesses
            };
            endProcessItem.Click += (_, _) => _viewModel.EndSelectedProcesses();
            menu.Items.Add(endProcessItem);

            var endTreeItem = new AtomUI.Desktop.Controls.MenuItem
            {
                Header = _viewModel.EndProcessTreeText,
                IsEnabled = _viewModel.HasSelectedProcesses
            };
            endTreeItem.Click += (_, _) => _viewModel.EndSelectedProcessTrees();
            menu.Items.Add(endTreeItem);

            var endAssociatedItem = new AtomUI.Desktop.Controls.MenuItem
            {
                Header = _viewModel.EndAssociatedProcessesText,
                IsEnabled = _viewModel.HasSelectedProcesses
            };
            endAssociatedItem.Click += (_, _) => _viewModel.EndSelectedAssociatedProcesses();
            menu.Items.Add(endAssociatedItem);

            menu.ShowAt(dataGrid);
        }

        private void ShowColumnMenu(DataGrid dataGrid)
        {
            if (_viewModel is null)
            {
                return;
            }

            var menu = new AtomUI.Desktop.Controls.MenuFlyout
            {
                Placement = PlacementMode.Pointer,
                IsMotionEnabled = true
            };

            menu.Items.Add(new AtomUI.Desktop.Controls.MenuItem
            {
                Header = _viewModel.ColumnVisibilityText,
                IsEnabled = false
            });
            menu.Items.Add(new MenuSeparator());

            foreach (var option in _viewModel.ProcessColumns)
            {
                var item = new AtomUI.Desktop.Controls.MenuItem
                {
                    Header = option.Header,
                    ToggleType = MenuItemToggleType.CheckBox,
                    IsChecked = option.IsVisible,
                    IsEnabled = !option.IsRequired
                };
                item.Click += (_, _) =>
                {
                    _viewModel.SetProcessColumnVisibility(option.Key, item.IsChecked == true);
                    ApplyColumnVisibility();
                };
                menu.Items.Add(item);
            }

            menu.ShowAt(dataGrid);
        }

        private void ApplyColumnVisibility()
        {
            var processGrid = GetProcessGrid();
            if (_viewModel is null || processGrid is null || processGrid.Columns.Count < 9)
            {
                return;
            }

            processGrid.Columns[0].IsVisible = true;
            processGrid.Columns[1].IsVisible = true;
            processGrid.Columns[2].IsVisible = true;
            processGrid.Columns[3].IsVisible = _viewModel.IsProcessColumnVisible(MainWindowViewModel.ProcessColumnPublisher);
            processGrid.Columns[4].IsVisible = _viewModel.IsProcessColumnVisible(MainWindowViewModel.ProcessColumnCpu);
            processGrid.Columns[5].IsVisible = _viewModel.IsProcessColumnVisible(MainWindowViewModel.ProcessColumnMemory);
            processGrid.Columns[6].IsVisible = _viewModel.IsProcessColumnVisible(MainWindowViewModel.ProcessColumnDisk);
            processGrid.Columns[7].IsVisible = _viewModel.IsProcessColumnVisible(MainWindowViewModel.ProcessColumnNetwork);
            processGrid.Columns[8].IsVisible = _viewModel.IsProcessColumnVisible(MainWindowViewModel.ProcessColumnGpu);
        }

        private DataGrid? GetProcessGrid()
        {
            return ProcessGrid ?? this.FindControl<DataGrid>("ProcessGrid");
        }

        private void HandleColumnOptionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProcessColumnOptionViewModel.IsVisible))
            {
                ApplyColumnVisibility();
            }
        }

        private void HandleProcessColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems is not null)
            {
                foreach (var option in e.OldItems.OfType<ProcessColumnOptionViewModel>())
                {
                    option.PropertyChanged -= HandleColumnOptionChanged;
                }
            }

            if (e.NewItems is not null)
            {
                foreach (var option in e.NewItems.OfType<ProcessColumnOptionViewModel>())
                {
                    option.PropertyChanged += HandleColumnOptionChanged;
                }
            }

            ApplyColumnVisibility();
        }

        private static bool IsColumnHeaderSource(Visual source, DataGrid dataGrid)
        {
            for (var current = source; current is not null; current = current.GetVisualParent())
            {
                if (ReferenceEquals(current, dataGrid))
                {
                    return false;
                }

                if (current.GetType().Name.Contains("DataGridColumnHeader", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
