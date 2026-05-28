using NexusDash.Models;
using ReactiveUI;
using System;
using System.Globalization;

namespace NexusDash.ViewModels
{
    public sealed class FileSearchResultViewModel : ReactiveObject
    {
        private string _typeText;

        public FileSearchResultViewModel(
            FileSearchResult result,
            string fileTypeText,
            string folderTypeText)
        {
            Name = result.Name;
            DirectoryPath = result.DirectoryPath;
            FullPath = result.FullPath;
            IsDirectory = result.IsDirectory;
            SizeText = result.SizeBytes is { } sizeBytes
                ? ProcessRowViewModel.FormatBytes((ulong)Math.Max(0, sizeBytes))
                : "-";
            ModifiedText = result.LastWriteTime?.ToString("g", CultureInfo.CurrentCulture) ?? "-";
            _typeText = result.IsDirectory ? folderTypeText : fileTypeText;
        }

        public string Name { get; }
        public string DirectoryPath { get; }
        public string FullPath { get; }
        public bool IsDirectory { get; }
        public string SizeText { get; }
        public string ModifiedText { get; }

        public string TypeText
        {
            get => _typeText;
            private set => this.RaiseAndSetIfChanged(ref _typeText, value);
        }

        public void RefreshLocalizedText(string fileTypeText, string folderTypeText)
        {
            TypeText = IsDirectory ? folderTypeText : fileTypeText;
        }
    }
}
