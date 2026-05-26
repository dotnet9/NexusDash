using Lang.Avalonia;
using CodeWF.EventBus;
using NexusDash.Services;
using ReactiveUI;
using System;

namespace NexusDash.ViewModels.Settings
{
    public abstract class SettingsPageViewModelBase : ReactiveObject, IDisposable
    {
        private readonly IEventBus _eventBus;
        private bool _isDisposed;
        private bool _isDarkTheme = true;
        private string _cultureName = "zh-CN";

        protected SettingsPageViewModelBase(IEventBus eventBus, IUserPreferencesService userPreferencesService)
        {
            _eventBus = eventBus;
            var preferences = userPreferencesService.Load();
            _isDarkTheme = preferences.IsDarkTheme;
            _cultureName = preferences.CultureName;
            _eventBus.Subscribe(this);
        }

        protected IEventBus EventBus => _eventBus;
        protected bool IsDarkThemeState => _isDarkTheme;
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
            _isDarkTheme = command.IsDarkTheme;
            _cultureName = command.CultureName;
            RaiseLocalizedProperties();
        }
    }
}
