using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using NexusDash.ViewModels;

namespace NexusDash
{
    public partial class MainWindow : Window
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
    }
}
