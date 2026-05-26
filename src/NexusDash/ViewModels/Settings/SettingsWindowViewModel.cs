using CodeWF.EventBus;
using NexusDash;
using ReactiveUI;
using System;
using Lang.Avalonia;

namespace NexusDash.ViewModels.Settings
{
    public sealed class SettingsWindowViewModel : ReactiveObject, IDisposable
    {
        private readonly IEventBus _eventBus;
        private bool _isDisposed;

        public SettingsWindowViewModel(IEventBus eventBus)
        {
            _eventBus = eventBus;
            _eventBus.Subscribe(this);
        }

        public string SettingsText => I18nManager.Instance.GetResource(NexusDashL.Settings) ?? NexusDashL.Settings;

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _eventBus.Unsubscribe(this);
            _isDisposed = true;
        }

        [EventHandler]
        private void ApplySettingsState(SettingsStateChangedCommand command)
        {
            this.RaisePropertyChanged(nameof(SettingsText));
        }
    }
}
