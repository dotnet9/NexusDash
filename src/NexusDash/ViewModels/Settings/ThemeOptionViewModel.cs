using Avalonia.Media;
using ReactiveUI;

namespace NexusDash.ViewModels.Settings
{
    public sealed class ThemeOptionViewModel(string key, string displayName, IBrush accentBrush) : ReactiveObject
    {
        private string _displayName = displayName;

        public string Key { get; } = key;
        public IBrush AccentBrush { get; } = accentBrush;

        public string DisplayName
        {
            get => _displayName;
            set => this.RaiseAndSetIfChanged(ref _displayName, value);
        }
    }
}
