using CodeWF.Log.Core;
using Lang.Avalonia;
using NexusDash.Services;
using Prism.Commands;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace NexusDash.ViewModels
{
    public sealed class HardwareInfoViewModel : ReactiveObject, IDisposable
    {
        private readonly HardwareInfoService _hardwareInfoService;
        private CancellationTokenSource? _refreshCancellation;
        private string _statusMessage;
        private DateTime? _lastUpdated;
        private bool _isLoading;
        private bool _isDisposed;

        public HardwareInfoViewModel(HardwareInfoService hardwareInfoService)
        {
            _hardwareInfoService = hardwareInfoService;
            _statusMessage = T(NexusDashL.HardwareStatusReady);
            RefreshHardwareInfo = new DelegateCommand(
                () => _ = RefreshAsync(),
                () => !IsLoading);
            _ = RefreshAsync();
        }

        public ObservableCollection<HardwareInfoSectionViewModel> Sections { get; } = new();
        public DelegateCommand RefreshHardwareInfo { get; }
        public string HardwareInfoText => T(NexusDashL.HardwareInfo);
        public string RefreshText => T(NexusDashL.HardwareRefresh);
        public string StatusText => _statusMessage;
        public string LastUpdatedText => _lastUpdated is null
            ? ""
            : string.Format(
                CultureInfo.CurrentCulture,
                T(NexusDashL.HardwareLastUpdated),
                _lastUpdated.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture));
        public bool HasLastUpdated => _lastUpdated is not null;
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (SetField(ref _isLoading, value, nameof(IsLoading)))
                {
                    RefreshHardwareInfo.RaiseCanExecuteChanged();
                }
            }
        }

        public async Task RefreshAsync()
        {
            if (_isDisposed || IsLoading)
            {
                return;
            }

            _refreshCancellation?.Cancel();
            _refreshCancellation?.Dispose();
            var cancellation = new CancellationTokenSource();
            _refreshCancellation = cancellation;

            IsLoading = true;
            StatusMessage = T(NexusDashL.HardwareStatusLoading);

            try
            {
                var snapshot = await _hardwareInfoService.CaptureAsync(cancellation.Token);
                if (_isDisposed || cancellation.IsCancellationRequested)
                {
                    return;
                }

                ReplaceSections(snapshot.Sections);
                _lastUpdated = DateTime.Now;
                this.RaisePropertyChanged(nameof(LastUpdatedText));
                this.RaisePropertyChanged(nameof(HasLastUpdated));
                StatusMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    T(NexusDashL.HardwareStatusLoaded),
                    Sections.Count);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                StatusMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    T(NexusDashL.HardwareStatusFailed),
                    exception.Message);
                Logger.Error(
                    "Hardware information refresh failed.",
                    exception,
                    StatusMessage);
            }
            finally
            {
                if (ReferenceEquals(_refreshCancellation, cancellation))
                {
                    _refreshCancellation = null;
                }

                if (!_isDisposed)
                {
                    IsLoading = false;
                }

                cancellation.Dispose();
            }
        }

        public void RefreshLocalizedText()
        {
            this.RaisePropertyChanged(nameof(HardwareInfoText));
            this.RaisePropertyChanged(nameof(RefreshText));
            this.RaisePropertyChanged(nameof(StatusText));
            this.RaisePropertyChanged(nameof(LastUpdatedText));
            this.RaisePropertyChanged(nameof(HasLastUpdated));

            foreach (var section in Sections)
            {
                section.RefreshLocalizedText();
            }

            if (!IsLoading && _lastUpdated is null)
            {
                StatusMessage = T(NexusDashL.HardwareStatusReady);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _refreshCancellation?.Cancel();
            _refreshCancellation?.Dispose();
            _refreshCancellation = null;
        }

        private string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (SetField(ref _statusMessage, value, nameof(StatusText)))
                {
                    Logger.Info(value);
                }
            }
        }

        private void ReplaceSections(IReadOnlyList<HardwareInfoSectionSnapshot> sections)
        {
            Sections.Clear();
            foreach (var section in sections)
            {
                Sections.Add(new HardwareInfoSectionViewModel(section));
            }
        }

        private bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            this.RaiseAndSetIfChanged(ref field, value, propertyName);
            return true;
        }

        private static string T(string key)
        {
            return I18nManager.Instance.GetResource(key) ?? key;
        }
    }

    public sealed class HardwareInfoSectionViewModel : ReactiveObject
    {
        private readonly string _titleKey;

        public HardwareInfoSectionViewModel(HardwareInfoSectionSnapshot section)
        {
            _titleKey = section.TitleKey;
            foreach (var item in section.Items)
            {
                Items.Add(new HardwareInfoItemViewModel(item));
            }
        }

        public ObservableCollection<HardwareInfoItemViewModel> Items { get; } = new();
        public string Title => T(_titleKey);

        public void RefreshLocalizedText()
        {
            this.RaisePropertyChanged(nameof(Title));
            foreach (var item in Items)
            {
                item.RefreshLocalizedText();
            }
        }

        private static string T(string key)
        {
            return I18nManager.Instance.GetResource(key) ?? key;
        }
    }

    public sealed class HardwareInfoItemViewModel : ReactiveObject
    {
        private readonly string _nameKey;
        private readonly string? _displayName;

        public HardwareInfoItemViewModel(HardwareInfoItemSnapshot item)
        {
            _nameKey = item.NameKey;
            _displayName = item.DisplayName;
            Value = item.Value;
        }

        public string Name => string.IsNullOrWhiteSpace(_displayName)
            ? T(_nameKey)
            : _displayName;
        public string Value { get; }

        public void RefreshLocalizedText()
        {
            this.RaisePropertyChanged(nameof(Name));
        }

        private static string T(string key)
        {
            return I18nManager.Instance.GetResource(key) ?? key;
        }
    }
}
