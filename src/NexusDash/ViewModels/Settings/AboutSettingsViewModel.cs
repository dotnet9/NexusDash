using NexusDash;
using NexusDash.ViewModels;
using ReactiveUI;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NexusDash.ViewModels.Settings
{
    public sealed class AboutSettingsViewModel(MainWindowViewModel mainViewModel) : SettingsPageViewModelBase(mainViewModel)
    {
        private static readonly Assembly AppAssembly = typeof(AboutSettingsViewModel).Assembly;

        public override string Header => T(NexusDashL.SettingsAbout);
        public override int Order => 30;
        public string AppName => T(NexusDashL.AppName);
        public string Description => T(NexusDashL.AboutDescription);
        public string VersionLabel => T(NexusDashL.AboutVersion);
        public string CompileTimeLabel => T(NexusDashL.AboutCompileTime);
        public string AuthorLabel => T(NexusDashL.AboutAuthor);
        public string RepositoryLabel => T(NexusDashL.AboutRepository);
        public string LicenseLabel => T(NexusDashL.AboutLicense);
        public string CopyrightLabel => T(NexusDashL.AboutCopyright);
        public string Version => GetInformationalVersion();
        public string CompileTime => GetCompileTime();
        public string Author => GetAssemblyMetadata("Author", "沙漠尽头的狼");
        public string RepositoryUrl => GetAssemblyMetadata("ProjectUrl", "https://codewf.com");
        public string License => GetAssemblyMetadata("License", "MIT");
        public string Copyright =>
            AppAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright
            ?? $"Copyright (c) {DateTime.Now.Year} {Author}";
        public Uri RepositoryUri => new(RepositoryUrl);

        protected override void RaiseLocalizedProperties()
        {
            this.RaisePropertyChanged(nameof(Header));
            this.RaisePropertyChanged(nameof(AppName));
            this.RaisePropertyChanged(nameof(Description));
            this.RaisePropertyChanged(nameof(VersionLabel));
            this.RaisePropertyChanged(nameof(CompileTimeLabel));
            this.RaisePropertyChanged(nameof(AuthorLabel));
            this.RaisePropertyChanged(nameof(RepositoryLabel));
            this.RaisePropertyChanged(nameof(LicenseLabel));
            this.RaisePropertyChanged(nameof(CopyrightLabel));
        }

        private static string GetInformationalVersion()
        {
            var informationalVersion = AppAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            return string.IsNullOrWhiteSpace(informationalVersion)
                ? AppAssembly.GetName().Version?.ToString() ?? ""
                : informationalVersion;
        }

        private static string GetAssemblyMetadata(string key, string fallback)
        {
            var value = AppAssembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.OrdinalIgnoreCase))
                ?.Value;
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string GetCompileTime()
        {
            try
            {
                var location = AppAssembly.Location;
                return string.IsNullOrWhiteSpace(location)
                    ? ""
                    : File.GetLastWriteTime(location).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
            catch
            {
                return "";
            }
        }
    }
}
