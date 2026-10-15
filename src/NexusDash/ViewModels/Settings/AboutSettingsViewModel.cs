using NexusDash;
using CodeWF.Toolkit.EventBus;
using NexusDash.Services;
using CodeWF.Toolkit.Core.UpdateChecking;
using CodeWF.Toolkit.Core.Extensions;
using System.Runtime.InteropServices;
using Prism.Commands;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace NexusDash.ViewModels.Settings
{
    public sealed class AboutSettingsViewModel(
        IEventBus eventBus,
        IUserPreferencesService userPreferencesService)
        : SettingsPageViewModelBase(eventBus, userPreferencesService)
    {
        private static readonly Assembly AppAssembly = typeof(AboutSettingsViewModel).Assembly;
        public UpdateViewModel Update { get; } = new();

        public override string Header => T(NexusDashL.SettingsAbout);
        public override int Order => 30;
        public DelegateCommand OpenRepositoryCommand { get; } = new(OpenRepository);

        public string AppName => T(NexusDashL.AppName);
        public string Description => T(NexusDashL.AboutDescription);
        public string VersionLabel => T(NexusDashL.AboutVersion);
        public string CompileTimeLabel => T(NexusDashL.AboutCompileTime);
        public string AuthorLabel => T(NexusDashL.AboutAuthor);
        public string RepositoryLabel => T(NexusDashL.AboutRepository);
        public string LicenseLabel => T(NexusDashL.AboutLicense);
        public string CopyrightLabel => T(NexusDashL.AboutCopyright);
        public string Version => UpdateVersion.Display(AppAssembly);
        public string PlatformLabel => T(NexusDashL.AboutPlatform);
        public string Platform => $"{RuntimeInformation.FrameworkDescription} · Avalonia {typeof(Avalonia.Application).Assembly.GetName().Version}";
        public string CompileTime => AppAssembly.CompileTime()?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "—";
        public string Author => T(NexusDashL.AboutAuthorName);
        public string RepositoryUrl => "https://github.com/dotnet9/NexusDash";
        public string License => GetAssemblyMetadata("License", "MIT");
        public string Copyright =>
            AppAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright
            ?? $"Copyright (c) {DateTime.Now.Year} {Author}";
        public Uri RepositoryUri => new(RepositoryUrl);

        private static void OpenRepository()
        {
            var repositoryUrl = "https://github.com/dotnet9/NexusDash";
            Process.Start(new ProcessStartInfo(repositoryUrl)
            {
                UseShellExecute = true
            });
        }

        protected override void RaiseLocalizedProperties()
        {
            this.RaisePropertyChanged(nameof(Header));
            this.RaisePropertyChanged(nameof(PlatformLabel));
            this.RaisePropertyChanged(nameof(Author));
            this.RaisePropertyChanged(nameof(AppName));
            this.RaisePropertyChanged(nameof(Description));
            this.RaisePropertyChanged(nameof(VersionLabel));
            this.RaisePropertyChanged(nameof(CompileTimeLabel));
            this.RaisePropertyChanged(nameof(AuthorLabel));
            this.RaisePropertyChanged(nameof(RepositoryLabel));
            this.RaisePropertyChanged(nameof(LicenseLabel));
            this.RaisePropertyChanged(nameof(CopyrightLabel));
        }

        public override void Dispose()
        {
            Update.Dispose();
            base.Dispose();
        }

        private static string GetAssemblyMetadata(string key, string fallback)
        {
            var value = AppAssembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.OrdinalIgnoreCase))
                ?.Value;
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

    }
}
