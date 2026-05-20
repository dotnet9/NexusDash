using ReactiveUI;

namespace NexusDash.ViewModels
{
    public sealed class LanguageOption : ReactiveObject
    {
        private string _displayName;

        public LanguageOption(string cultureName, string displayName)
        {
            CultureName = cultureName;
            _displayName = displayName;
        }

        public string CultureName { get; }

        public string DisplayName
        {
            get => _displayName;
            set => this.RaiseAndSetIfChanged(ref _displayName, value);
        }
    }
}
