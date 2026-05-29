using Lang.Avalonia;
using CodeWF.EventBus;
using NexusDash.Services;
using ReactiveUI;
using System;
using System.Globalization;

namespace NexusDash.ViewModels.Settings
{
    public abstract class SettingsPageViewModelBase : ReactiveObject, IDisposable
    {
        private readonly IEventBus _eventBus;
        private bool _isDisposed;
        private bool _isDarkTheme = true;
        private bool _rememberWindowSize;
        private string _themeKey = ThemeResourceService.DarkThemeKey;
        private string _cultureName = "";

        protected SettingsPageViewModelBase(IEventBus eventBus, IUserPreferencesService userPreferencesService)
        {
            _eventBus = eventBus;
            var preferences = userPreferencesService.Load();
            _themeKey = ThemeResourceService.ResolvePreferenceThemeKey(preferences.ThemeKey, preferences.IsDarkTheme);
            _isDarkTheme = preferences.IsDarkTheme;
            _rememberWindowSize = preferences.RememberWindowSize;
            _cultureName = preferences.CultureName ??
                           I18nManager.Instance.Culture?.Name ??
                           CultureInfo.CurrentUICulture.Name;
            _eventBus.Subscribe(this);
        }

        protected IEventBus EventBus => _eventBus;
        protected bool IsDarkThemeState => _isDarkTheme;
        protected bool RememberWindowSizeState => _rememberWindowSize;
        protected string ThemeKeyState => _themeKey;
        protected string CultureNameState => _cultureName;

        public abstract string Header { get; }

        public virtual int Order => int.MaxValue;

        public virtual void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _eventBus.Unsubscribe(this);
            _isDisposed = true;
        }

        protected static string T(string key)
        {
            return I18nManager.Instance.GetResource(key) ?? key;
        }

        protected abstract void RaiseLocalizedProperties();

        [EventHandler]
        private void ApplySettingsState(SettingsStateChangedCommand command)
        {
            _themeKey = command.ThemeKey;
            _isDarkTheme = command.IsDarkTheme;
            _rememberWindowSize = command.RememberWindowSize;
            _cultureName = command.CultureName;
            RaiseLocalizedProperties();
        }
    }
}
