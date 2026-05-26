using NexusDash;
using CodeWF.EventBus;
using NexusDash.Services;
using ReactiveUI;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace NexusDash.ViewModels.Settings
{
    public sealed class ChangelogSettingsViewModel : SettingsPageViewModelBase
    {
        private const string ChineseChangelogFileName = "CHANGELOG.zh-CN.md";
        private const string EnglishChangelogFileName = "CHANGELOG.md";

        private string _markdown = "";

        public ChangelogSettingsViewModel(
            IEventBus eventBus,
            IUserPreferencesService userPreferencesService)
            : base(eventBus, userPreferencesService)
        {
            if (CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                LoadChinese();
            }
            else
            {
                LoadEnglish();
            }
        }

        public override string Header => T(NexusDashL.SettingsChangelog);
        public override int Order => 20;
        public string ChineseText => T(NexusDashL.ChangelogChinese);
        public string EnglishText => T(NexusDashL.ChangelogEnglish);

        public string Markdown
        {
            get => _markdown;
            private set => this.RaiseAndSetIfChanged(ref _markdown, value);
        }

        public void LoadChinese()
        {
            LoadChangelog(ChineseChangelogFileName);
        }

        public void LoadEnglish()
        {
            LoadChangelog(EnglishChangelogFileName);
        }

        protected override void RaiseLocalizedProperties()
        {
            this.RaisePropertyChanged(nameof(Header));
            this.RaisePropertyChanged(nameof(ChineseText));
            this.RaisePropertyChanged(nameof(EnglishText));
        }

        private void LoadChangelog(string fileName)
        {
            var path = FindBundledFile(fileName);
            Markdown = path is null
                ? $"# {Header}\n\n{string.Format(CultureInfo.CurrentCulture, T(NexusDashL.ChangelogMissing), fileName)}"
                : File.ReadAllText(path, Encoding.UTF8);
        }

        private static string? FindBundledFile(string fileName)
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, fileName),
                Path.Combine(Directory.GetCurrentDirectory(), fileName),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", fileName)),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", fileName))
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
