using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CodeWF.AvaloniaControls.Controls;
using CodeWF.Log.Core;
using NexusDash.Services;
using System;
using System.Linq;

namespace NexusDash
{
    public partial class MainWindow : CodeWFWindow
    {
        private static readonly (double Width, double Height)[] SupersededDefaultWindowSizes =
        [
            (1280, 820),
            (1440, 820),
            (1440, 900)
        ];

        private IUserPreferencesService? _userPreferencesService;

        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(IUserPreferencesService userPreferencesService)
        {
            _userPreferencesService = userPreferencesService;
            InitializeComponent();
            ApplyWindowPreferences();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            SaveWindowPreferences();
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }

            Logger.Info("NexusDash application closing.", "NexusDash application closing.", log2Console: false);
            FlushOperationLogs();

            base.OnClosing(e);
        }

        private void ApplyWindowPreferences()
        {
            if (_userPreferencesService is null)
            {
                return;
            }

            var preferences = _userPreferencesService.Load();
            if (!preferences.RememberWindowSize)
            {
                return;
            }

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

            if (!_userPreferencesService.Load().RememberWindowSize)
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

        private static void FlushOperationLogs()
        {
            try
            {
                Logger.FlushAsync().Wait(TimeSpan.FromMilliseconds(500));
            }
            catch
            {
            }
        }
    }
}
