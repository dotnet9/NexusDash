using Lang.Avalonia;
using Avalonia.Threading;
using CodeWF.Log.Core;
using NexusDash.Models;
using NexusDash.Services;
using Prism.Commands;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NexusDash.ViewModels
{
    public sealed class FileSearchViewModel : ReactiveObject, IDisposable
    {
        private const int MaxResults = 1000;

        private readonly FileSearchService _fileSearchService;
        private CancellationTokenSource? _searchCancellation;
        private string _searchQuery = "";
        private string _searchRootText;
        private string _statusMessage;
        private bool _isSearching;
        private bool _hasSearched;
        private bool _isDisposed;
        private int _searchVersion;

        public FileSearchViewModel(FileSearchService fileSearchService)
        {
            _fileSearchService = fileSearchService;
            _searchRootText = string.Join("; ", _fileSearchService.GetDefaultSearchRoots());
            _statusMessage = T(NexusDashL.FileSearchStatusReady);
            SearchFiles = new DelegateCommand(
                () => _ = SearchAsync(),
                CanStartSearch);
            CancelSearch = new DelegateCommand(
                CancelCurrentSearch,
                () => IsSearching);
        }

        public ObservableCollection<FileSearchResultViewModel> Results { get; } = new();
        public DelegateCommand SearchFiles { get; }
        public DelegateCommand CancelSearch { get; }

        public string FileSearchText => T(NexusDashL.FileSearch);
        public string SearchLocationText => T(NexusDashL.FileSearchLocation);
        public string SearchPlaceholderText => T(NexusDashL.FileSearchPlaceholder);
        public string SearchRootPlaceholderText => T(NexusDashL.FileSearchRootPlaceholder);
        public string SearchButtonText => T(NexusDashL.FileSearchButton);
        public string CancelButtonText => T(NexusDashL.FileSearchCancel);
        public string NameColumnText => T(NexusDashL.FileSearchName);
        public string DirectoryColumnText => T(NexusDashL.FileSearchDirectory);
        public string FullPathColumnText => T(NexusDashL.FileSearchFullPath);
        public string TypeColumnText => T(NexusDashL.FileSearchType);
        public string SizeColumnText => T(NexusDashL.FileSearchSize);
        public string ModifiedColumnText => T(NexusDashL.FileSearchModified);
        public string OpenContainingDirectoryText => T(NexusDashL.FileSearchOpenContainingDirectory);
        public string NoResultsText => T(NexusDashL.FileSearchNoResults);
        public string ResultCountText => string.Format(
            CultureInfo.CurrentCulture,
            T(NexusDashL.FileSearchResultCount),
            Results.Count);
        public bool HasResults => Results.Count > 0;
        public bool HasNoResults => _hasSearched && !IsSearching && Results.Count == 0;

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetField(ref _searchQuery, value ?? "", nameof(SearchQuery)))
                {
                    RaiseSearchStateProperties();
                }
            }
        }

        public string SearchRootText
        {
            get => _searchRootText;
            set
            {
                if (SetField(ref _searchRootText, value ?? "", nameof(SearchRootText)))
                {
                    RaiseSearchStateProperties();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (SetField(ref _statusMessage, value, nameof(StatusMessage)))
                {
                    LogInfo(value);
                }
            }
        }

        public bool IsSearching
        {
            get => _isSearching;
            private set
            {
                if (SetField(ref _isSearching, value, nameof(IsSearching)))
                {
                    RaiseSearchStateProperties();
                }
            }
        }

        public void RefreshLocalizedText()
        {
            this.RaisePropertyChanged(nameof(FileSearchText));
            this.RaisePropertyChanged(nameof(SearchLocationText));
            this.RaisePropertyChanged(nameof(SearchPlaceholderText));
            this.RaisePropertyChanged(nameof(SearchRootPlaceholderText));
            this.RaisePropertyChanged(nameof(SearchButtonText));
            this.RaisePropertyChanged(nameof(CancelButtonText));
            this.RaisePropertyChanged(nameof(NameColumnText));
            this.RaisePropertyChanged(nameof(DirectoryColumnText));
            this.RaisePropertyChanged(nameof(FullPathColumnText));
            this.RaisePropertyChanged(nameof(TypeColumnText));
            this.RaisePropertyChanged(nameof(SizeColumnText));
            this.RaisePropertyChanged(nameof(ModifiedColumnText));
            this.RaisePropertyChanged(nameof(OpenContainingDirectoryText));
            this.RaisePropertyChanged(nameof(NoResultsText));
            this.RaisePropertyChanged(nameof(ResultCountText));

            foreach (var result in Results)
            {
                result.RefreshLocalizedText(T(NexusDashL.FileSearchFile), T(NexusDashL.FileSearchFolder));
            }

            if (!IsSearching && !_hasSearched)
            {
                StatusMessage = T(NexusDashL.FileSearchStatusReady);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            CancelCurrentSearch();
        }

        private bool CanStartSearch()
        {
            return !IsSearching &&
                   !string.IsNullOrWhiteSpace(SearchQuery) &&
                   ParseRoots(SearchRootText).Count > 0;
        }

        private async Task SearchAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            if (!CanStartSearch())
            {
                StatusMessage = string.IsNullOrWhiteSpace(SearchQuery)
                    ? T(NexusDashL.FileSearchEmptyQuery)
                    : T(NexusDashL.FileSearchRootsUnavailable);
                return;
            }

            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            var cancellation = new CancellationTokenSource();
            _searchCancellation = cancellation;
            var searchVersion = ++_searchVersion;
            var query = SearchQuery.Trim();
            var roots = ParseRoots(SearchRootText);

            IsSearching = true;
            _hasSearched = true;
            ReplaceResults([]);
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                T(NexusDashL.FileSearchStatusSearching),
                query);
            Logger.Info(
                $"File search started: query={query}; roots={string.Join("; ", roots)}; maxResults={MaxResults}",
                $"文件搜索开始：{query}，位置：{string.Join("; ", roots)}",
                log2Console: false);

            try
            {
                var resultCount = await _fileSearchService.SearchByFileNameAsync(
                    query,
                    roots,
                    MaxResults,
                    result => QueueSearchResult(result, cancellation, searchVersion),
                    cancellation.Token);
                if (cancellation.IsCancellationRequested)
                {
                    UpdateCancelledStatus(query);
                    return;
                }

                if (_isDisposed)
                {
                    return;
                }

                StatusMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    T(NexusDashL.FileSearchStatusCompleted),
                    resultCount,
                    query);
                Logger.Info(
                    $"File search completed: query={query}; results={resultCount}",
                    $"文件搜索完成：{query}，找到 {resultCount} 个结果",
                    log2Console: false);
            }
            catch (OperationCanceledException)
            {
                UpdateCancelledStatus(query);
            }
            catch (Exception exception)
            {
                if (_isDisposed)
                {
                    return;
                }

                StatusMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    T(NexusDashL.FileSearchStatusFailed),
                    exception.Message);
                Logger.Error(
                    $"File search failed: query={query}; roots={string.Join("; ", roots)}",
                    exception,
                    StatusMessage,
                    log2Console: false);
            }
            finally
            {
                if (ReferenceEquals(_searchCancellation, cancellation))
                {
                    _searchCancellation = null;
                }

                if (_isDisposed)
                {
                    _isSearching = false;
                }
                else
                {
                    IsSearching = false;
                }

                cancellation.Dispose();
            }
        }

        public void CancelCurrentSearch()
        {
            _searchCancellation?.Cancel();
        }

        private void UpdateCancelledStatus(string query)
        {
            if (_isDisposed)
            {
                return;
            }

            StatusMessage = T(NexusDashL.FileSearchStatusCancelled);
            Logger.Warn(
                $"File search cancelled: query={query}",
                $"文件搜索已取消：{query}",
                log2Console: false);
        }

        private void QueueSearchResult(
            FileSearchResult result,
            CancellationTokenSource cancellation,
            int searchVersion)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_isDisposed ||
                    cancellation.IsCancellationRequested ||
                    searchVersion != _searchVersion)
                {
                    return;
                }

                Results.Add(new FileSearchResultViewModel(
                    result,
                    T(NexusDashL.FileSearchFile),
                    T(NexusDashL.FileSearchFolder)));
                RaiseResultStateProperties();
            });
        }

        private void ReplaceResults(IReadOnlyList<FileSearchResultViewModel> results)
        {
            Results.Clear();
            foreach (var result in results)
            {
                Results.Add(result);
            }

            RaiseResultStateProperties();
        }

        private void RaiseSearchStateProperties()
        {
            SearchFiles.RaiseCanExecuteChanged();
            CancelSearch.RaiseCanExecuteChanged();
            this.RaisePropertyChanged(nameof(HasNoResults));
        }

        private void RaiseResultStateProperties()
        {
            this.RaisePropertyChanged(nameof(ResultCountText));
            this.RaisePropertyChanged(nameof(HasResults));
            this.RaisePropertyChanged(nameof(HasNoResults));
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

        private static IReadOnlyList<string> ParseRoots(string rootText)
        {
            return rootText
                .Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string T(string key)
        {
            return I18nManager.Instance.GetResource(key) ?? key;
        }

        private static void LogInfo(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Logger.Info(message, message, log2Console: false);
            }
        }
    }
}
