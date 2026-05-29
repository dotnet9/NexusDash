using Avalonia.Media;
using ReactiveUI;

namespace NexusDash.ViewModels.Settings
{
    public sealed class ThemeOptionViewModel(string key, string displayName, string accentColor) : ReactiveObject
    {
        private string _displayName = displayName;

        public string Key { get; } = key;
        public IBrush AccentBrush { get; } = new SolidColorBrush(Color.Parse(accentColor));

        public string DisplayName
        {
            get => _displayName;
            set => this.RaiseAndSetIfChanged(ref _displayName, value);
        }
    }
}
