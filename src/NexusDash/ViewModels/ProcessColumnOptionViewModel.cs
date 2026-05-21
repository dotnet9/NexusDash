using ReactiveUI;
using System;
using System.Collections.Generic;

namespace NexusDash.ViewModels
{
    public sealed class ProcessColumnOptionViewModel : ReactiveObject
    {
        private readonly Action<ProcessColumnOptionViewModel> _visibilityChanged;
        private string _header;
        private bool _isVisible;

        public ProcessColumnOptionViewModel(
            string key,
            string header,
            bool isRequired,
            bool isVisible,
            Action<ProcessColumnOptionViewModel> visibilityChanged)
        {
            Key = key;
            _header = header;
            IsRequired = isRequired;
            _isVisible = isRequired || isVisible;
            _visibilityChanged = visibilityChanged;
        }

        public string Key { get; }

        public string Header
        {
            get => _header;
            private set => this.RaiseAndSetIfChanged(ref _header, value);
        }

        public bool IsRequired { get; }

        public bool IsVisible
        {
            get => IsRequired || _isVisible;
            set
            {
                if (IsRequired)
                {
                    value = true;
                }

                if (EqualityComparer<bool>.Default.Equals(_isVisible, value))
                {
                    return;
                }

                this.RaiseAndSetIfChanged(ref _isVisible, value);
                _visibilityChanged(this);
            }
        }

        public void RefreshHeader(string header)
        {
            Header = header;
        }
    }
}
