using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using NexusDash.Views;
using NexusDash.ViewModels;
using System;
using System.Linq;

namespace NexusDash
{
    public partial class MainWindow : AtomUI.Desktop.Controls.Window
    {
        private const double CompactTitleBarHeight = 30;

        private TitleBarLeftAddOn? _titleBarLeftAddOn;
        private IDisposable? _titleBarTitleBinding;
        private MainWindowViewModel? _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;
            DataContextChanged += (_, _) => ApplyTitleBarDataContext();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            _titleBarTitleBinding?.Dispose();
            _viewModel?.Dispose();
            base.OnClosing(e);
        }

        protected override WindowTitleBar? NotifyCreateTitleBar(WindowTitleBar? oldTitleBar)
        {
            return oldTitleBar ?? new WindowTitleBar
            {
                Name = "PART_TitleBar",
                Height = CompactTitleBarHeight,
                MinHeight = CompactTitleBarHeight,
                MaxHeight = CompactTitleBarHeight,
                Padding = new Thickness(8, 0),
                FontSize = 12
            };
        }

        protected override void NotifyConfigureTitleBar(WindowTitleBar titleBar)
        {
            base.NotifyConfigureTitleBar(titleBar);
            _titleBarLeftAddOn = new TitleBarLeftAddOn();
            ApplyTitleBarDataContext();
            _titleBarTitleBinding?.Dispose();
            _titleBarTitleBinding = titleBar.Bind(
                WindowTitleBar.TitleProperty,
                new Binding($"{nameof(DataContext)}.{nameof(MainWindowViewModel.WindowTitle)}")
                {
                    Source = this
                });
            titleBar.SetCurrentValue(WindowTitleBar.LeftAddOnProperty, _titleBarLeftAddOn);
            titleBar.SetCurrentValue(WindowTitleBar.RightAddOnProperty, null);
        }

        private void ApplyTitleBarDataContext()
        {
            if (_titleBarLeftAddOn is not null)
            {
                _titleBarLeftAddOn.DataContext = DataContext;
            }
        }

        private void ProcessList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_viewModel is null || sender is not Avalonia.Controls.ListBox listBox)
            {
                return;
            }

            _viewModel.SetSelectedProcesses(listBox.SelectedItems?.OfType<ProcessRowViewModel>() ?? []);
        }
    }
}
