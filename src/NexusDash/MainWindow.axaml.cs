using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using NexusDash.Services;
using NexusDash.Views;
using NexusDash.ViewModels;
using NexusDash.ViewModels.Settings;
using Prism.Regions;
using System;

namespace NexusDash
{
    public partial class MainWindow : AtomUI.Desktop.Controls.Window
    {
        private const double CompactTitleBarHeight = 40;

        private TitleBarSearchAddOn? _titleBarSearchAddOn;
        private IDisposable? _titleBarTitleBinding;
        private SettingsWindow? _settingsWindow;
        private MainWindowViewModel? _viewModel;
        private ProcessListView? _processListView;
        private readonly IRegionManager? _regionManager;

        public MainWindow()
            : this(new MainWindowViewModel(), null)
        {
        }

        public MainWindow(MainWindowViewModel viewModel, IRegionManager? regionManager)
        {
            _regionManager = regionManager;
            InitializeComponent();
            _processListView = this.FindControl<ProcessListView>("ProcessListPane");
            _viewModel = viewModel;
            DataContext = _viewModel;
            ApplyChildDataContexts();
            ApplyWindowPreferences();
            AddHandler(PointerPressedEvent, HandleTitleBarDragPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            DataContextChanged += (_, _) =>
            {
                ApplyTitleBarDataContext();
                ApplyChildDataContexts();
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            SaveWindowPreferences();
            _titleBarTitleBinding?.Dispose();
            _settingsWindow?.Close();
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
            _titleBarSearchAddOn = new TitleBarSearchAddOn();
            ApplyTitleBarDataContext();
            _titleBarTitleBinding?.Dispose();
            _titleBarTitleBinding = titleBar.Bind(
                WindowTitleBar.TitleProperty,
                new Binding($"{nameof(DataContext)}.{nameof(MainWindowViewModel.AppNameText)}")
                {
                    Source = this
                });
            titleBar.SetCurrentValue(WindowTitleBar.LeftAddOnProperty, null);
            titleBar.SetCurrentValue(WindowTitleBar.RightAddOnProperty, _titleBarSearchAddOn);
        }

        private void ApplyTitleBarDataContext()
        {
            if (_titleBarSearchAddOn is not null)
            {
                _titleBarSearchAddOn.DataContext = DataContext;
            }
        }

        private void ApplyChildDataContexts()
        {
            if (_processListView is not null)
            {
                _processListView.DataContext = _viewModel?.ProcessList;
            }
        }

        private void ApplyWindowPreferences()
        {
            var preferences = UserPreferencesService.Load();
            if (preferences.WindowWidth >= MinWidth)
            {
                Width = preferences.WindowWidth;
            }

            if (preferences.WindowHeight >= MinHeight)
            {
                Height = preferences.WindowHeight;
            }
        }

        private void SaveWindowPreferences()
        {
            if (WindowState != WindowState.Normal)
            {
                return;
            }

            UserPreferencesService.Update(preferences =>
            {
                preferences.WindowWidth = Math.Max(Width, MinWidth);
                preferences.WindowHeight = Math.Max(Height, MinHeight);
            });
        }

        private void SettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_viewModel is null)
            {
                return;
            }

            if (_settingsWindow is not null)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = new SettingsWindow(_regionManager?.CreateRegionManager() ?? _regionManager)
            {
                DataContext = new SettingsWindowViewModel(_viewModel)
            };
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show(this);
        }

        private void HandleTitleBarDragPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
                WindowState == WindowState.FullScreen ||
                !IsTitleBarDragSource(e))
            {
                return;
            }

            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                e.Handled = true;
                return;
            }

            BeginMoveDrag(e);
            e.Handled = true;
        }

        private bool IsTitleBarDragSource(PointerPressedEventArgs e)
        {
            var point = e.GetPosition(this);
            if (point.Y > CompactTitleBarHeight)
            {
                return false;
            }

            if (e.Source is not Visual source)
            {
                return true;
            }

            for (var current = source; current is not null; current = current.GetVisualParent())
            {
                var typeName = current.GetType().Name;
                if (current is Avalonia.Controls.TextBox ||
                    current is Avalonia.Controls.MenuItem ||
                    typeName.Contains("Button", StringComparison.Ordinal) ||
                    typeName.Contains("MenuItem", StringComparison.Ordinal) ||
                    typeName.Contains("CaptionButton", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
