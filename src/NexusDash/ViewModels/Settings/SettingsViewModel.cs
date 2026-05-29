using CodeWF.EventBus;
using NexusDash.ViewModels;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;

namespace NexusDash.ViewModels.Settings
{
    public sealed class SettingsViewModel : EventBusViewModel
    {
        private SettingsPageViewModelBase? _selectedPage;

        public SettingsViewModel(
            IEventBus eventBus,
            AppearanceSettingsViewModel appearance,
            ChangelogSettingsViewModel changelog,
            AboutSettingsViewModel about)
            : base(eventBus)
        {
            Pages.Add(appearance);
            Pages.Add(changelog);
            Pages.Add(about);
            SelectedPage = Pages.OrderBy(static page => page.Order).FirstOrDefault();
        }

        public ObservableCollection<SettingsPageViewModelBase> Pages { get; } = new();

        public SettingsPageViewModelBase? SelectedPage
        {
            get => _selectedPage;
            set => this.RaiseAndSetIfChanged(ref _selectedPage, value);
        }

        public override void Dispose()
        {
            foreach (var page in Pages)
            {
                page.Dispose();
            }

            base.Dispose();
        }
    }
}
