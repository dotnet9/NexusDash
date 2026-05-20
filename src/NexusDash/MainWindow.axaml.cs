using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using NexusDash.ViewModels;
using System.Linq;

namespace NexusDash
{
    public partial class MainWindow : AtomUI.Desktop.Controls.Window
    {
        private MainWindowViewModel? _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            _viewModel?.Dispose();
            base.OnClosing(e);
        }

        private void ProcessList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_viewModel is null || sender is not ListBox listBox)
            {
                return;
            }

            _viewModel.SetSelectedProcesses(listBox.SelectedItems?.OfType<ProcessRowViewModel>() ?? []);
        }
    }
}
