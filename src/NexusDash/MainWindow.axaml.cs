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
using System;
using System.Linq;

namespace NexusDash
{
    public partial class MainWindow : AtomUI.Desktop.Controls.Window
    {
        private const double CompactTitleBarHeight = 40;
        private const string TitleBarTitleBindingPath = "DataContext.AppNameText";
        private static readonly (double Width, double Height)[] SupersededDefaultWindowSizes =
        [
            (1280, 820),
            (1440, 820),
            (1440, 900)
        ];

        private TitleBarSearchAddOn? _titleBarSearchAddOn;
        private IDisposable? _titleBarTitleBinding;
        private IUserPreferencesService? _userPreferencesService;

        public MainWindow()
        {
            // Avalonia 运行时资源加载器需要公开无参构造；真实应用入口由 Prism 注入下方构造器。
            InitializeComponent();
        }

        public MainWindow(IUserPreferencesService userPreferencesService)
        {
            _userPreferencesService = userPreferencesService;
            InitializeComponent();
            ApplyWindowPreferences();
            AddHandler(PointerPressedEvent, HandleTitleBarDragPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            DataContextChanged += (_, _) => ApplyTitleBarDataContext();
            ApplyTitleBarDataContext();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            SaveWindowPreferences();
            _titleBarTitleBinding?.Dispose();
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }

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
                new Binding(TitleBarTitleBindingPath)
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

        private void ApplyWindowPreferences()
        {
            if (_userPreferencesService is null)
            {
                return;
            }

            var preferences = _userPreferencesService.Load();
            // 旧版本会把默认尺寸写进偏好；这些值不是用户主动调整，允许跟随新版窗口基准。
            if (IsSupersededDefaultWindowSize(preferences.WindowWidth, preferences.WindowHeight))
            {
                return;
            }

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
            if (WindowState != WindowState.Normal || _userPreferencesService is null)
            {
                return;
            }

            _userPreferencesService.Update(preferences =>
            {
                preferences.WindowWidth = Math.Max(Width, MinWidth);
                preferences.WindowHeight = Math.Max(Height, MinHeight);
            });
        }

        private static bool IsSupersededDefaultWindowSize(double width, double height)
        {
            return SupersededDefaultWindowSizes.Any(size =>
                size.Width.Equals(width) &&
                size.Height.Equals(height));
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
