using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CodeWF.EventBus;
using NexusDash.ViewModels;
using Prism.Ioc;
using System;

namespace NexusDash.Services
{
    public sealed class SettingsWindowService : ISettingsWindowService
    {
        private readonly IContainerProvider _container;
        private readonly IEventBus _eventBus;
        private SettingsWindow? _settingsWindow;
        private bool _isDisposed;

        public SettingsWindowService(IContainerProvider container, IEventBus eventBus)
        {
            _container = container;
            _eventBus = eventBus;
            _eventBus.Subscribe(this);
        }

        public void ShowSettingsWindow()
        {
            if (_settingsWindow is not null)
            {
                _settingsWindow.Activate();
                return;
            }

            _settingsWindow = _container.Resolve<SettingsWindow>();
            _settingsWindow.Closed += HandleSettingsWindowClosed;

            if (GetMainWindow() is { } owner)
            {
                _settingsWindow.Show(owner);
                return;
            }

            _settingsWindow.Show();
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _eventBus.Unsubscribe(this);
            CloseSettingsWindow();
        }

        [EventHandler]
        private void HandleOpenSettingsWindow(OpenSettingsWindowCommand command)
        {
            ShowSettingsWindow();
        }

        private void HandleSettingsWindowClosed(object? sender, EventArgs e)
        {
            if (_settingsWindow is not null)
            {
                _settingsWindow.Closed -= HandleSettingsWindowClosed;
                _settingsWindow = null;
            }
        }

        private void CloseSettingsWindow()
        {
            if (_settingsWindow is null)
            {
                return;
            }

            _settingsWindow.Closed -= HandleSettingsWindowClosed;
            _settingsWindow.Close();
            _settingsWindow = null;
        }

        private static Window? GetMainWindow()
        {
            return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
        }
    }
}
