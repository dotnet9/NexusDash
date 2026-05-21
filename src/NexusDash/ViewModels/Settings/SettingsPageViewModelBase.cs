using Lang.Avalonia;
using NexusDash.ViewModels;
using ReactiveUI;
using System;
using System.ComponentModel;

namespace NexusDash.ViewModels.Settings
{
    public abstract class SettingsPageViewModelBase : ReactiveObject, IDisposable
    {
        private bool _isDisposed;

        protected SettingsPageViewModelBase(MainWindowViewModel mainViewModel)
        {
            MainViewModel = mainViewModel;
            MainViewModel.PropertyChanged += HandleMainViewModelPropertyChanged;
        }

        protected MainWindowViewModel MainViewModel { get; }

        public abstract string Header { get; }

        public virtual int Order => int.MaxValue;

        public virtual void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            MainViewModel.PropertyChanged -= HandleMainViewModelPropertyChanged;
            _isDisposed = true;
        }

        protected static string T(string key)
        {
            return I18nManager.Instance.GetResource(key) ?? key;
        }

        protected abstract void RaiseLocalizedProperties();

        protected virtual bool ShouldRefreshFromMainPropertyChanged(string? propertyName)
        {
            return string.IsNullOrEmpty(propertyName) ||
                   propertyName == nameof(MainWindowViewModel.SettingsText);
        }

        private void HandleMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (ShouldRefreshFromMainPropertyChanged(e.PropertyName))
            {
                RaiseLocalizedProperties();
            }
        }
    }
}
