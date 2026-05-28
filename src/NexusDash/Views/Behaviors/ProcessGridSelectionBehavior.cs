using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;
using NexusDash.ViewModels;
using System.Linq;

namespace NexusDash.Views.Behaviors
{
    public sealed class ProcessGridSelectionBehavior : Behavior<DataGrid>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject is not null)
            {
                AssociatedObject.SelectionChanged += HandleSelectionChanged;
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject is not null)
            {
                AssociatedObject.SelectionChanged -= HandleSelectionChanged;
            }

            base.OnDetaching();
        }

        private void HandleSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (AssociatedObject is not { } dataGrid ||
                dataGrid.DataContext is not ProcessListViewModel viewModel ||
                viewModel.IsApplyingState)
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
            if (selectedRows.Length == 0 && viewModel.IsSelectedProcessStillVisible())
            {
                return;
            }

            viewModel.SetSelectedProcesses(selectedRows);
        }
    }
}
