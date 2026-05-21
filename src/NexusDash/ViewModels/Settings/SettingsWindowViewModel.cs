using NexusDash.ViewModels;
using ReactiveUI;
using System;
using System.ComponentModel;

namespace NexusDash.ViewModels.Settings
{
    public sealed class SettingsWindowViewModel : ReactiveObject, IDisposable
    {
        private readonly MainWindowViewModel _mainViewModel;
        private bool _isDisposed;

        public SettingsWindowViewModel(MainWindowViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            _mainViewModel.PropertyChanged += HandleMainViewModelPropertyChanged;
        }

        public string SettingsText => _mainViewModel.SettingsText;

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _mainViewModel.PropertyChanged -= HandleMainViewModelPropertyChanged;
            _isDisposed = true;
        }

        private void HandleMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                e.PropertyName == nameof(MainWindowViewModel.SettingsText))
            {
                this.RaisePropertyChanged(nameof(SettingsText));
            }
        }
    }
}
