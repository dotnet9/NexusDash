using ReactiveUI;

namespace NexusDash.ViewModels
{
    public sealed class ToolMenuNode(string header, string toolKey) : ReactiveObject
    {
        private string _header = header;

        public string Header
        {
            get => _header;
            set => this.RaiseAndSetIfChanged(ref _header, value);
        }

        public string ToolKey { get; } = toolKey;
    }
}
