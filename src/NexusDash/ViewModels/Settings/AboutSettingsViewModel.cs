using NexusDash;
using CodeWF.EventBus;
using NexusDash.Services;
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
        private static readonly UpdateChecker UpdateChecker = new("dotnet9", "NexusDash");
        private bool _isCheckingUpdate;
        private string _checkUpdateResult = string.Empty;

        public override string Header => T(NexusDashL.SettingsAbout);
        public override int Order => 30;
        public DelegateCommand OpenRepositoryCommand { get; } = new(OpenRepository);

        /// <summary>懒初始化：字段初始化器不能引用实例方法。</summary>
        public DelegateCommand CheckUpdateCommand => _checkUpdateCommand ??= new(async () => await CheckUpdateAsync());

        private DelegateCommand? _checkUpdateCommand;

        public string CheckUpdateLabel => T(NexusDashL.AboutCheckUpdate);
        public string CheckUpdateResult
        {
            get => _checkUpdateResult;
            private set => this.RaiseAndSetIfChanged(ref _checkUpdateResult, value);
        }
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

        private async Task CheckUpdateAsync()
        {
            if (_isCheckingUpdate)
            {
                return;
            }

            _isCheckingUpdate = true;
            try
            {
                CheckUpdateResult = T(NexusDashL.AboutCheckingUpdate);
                Version? current = UpdateVersion.Parse(GetInformationalVersion());
                UpdateCheckResult result = await UpdateChecker.CheckAsync(current ?? new Version(0, 0, 0));
                if (!result.Succeeded)
                {
                    CheckUpdateResult = string.Format(T(NexusDashL.AboutUpdateFailed), result.Error);
                    return;
                }

                if (result.Update is { } update)
                {
                    // 仅提醒不自动下载：提示并打开发布页
                    CheckUpdateResult = string.Format(T(NexusDashL.AboutUpdateAvailable), update.Tag);
                    Process.Start(new ProcessStartInfo(update.PageUrl) { UseShellExecute = true });
                    return;
                }

                CheckUpdateResult = T(NexusDashL.AboutUpToDate);
            }
            catch (Exception exception)
            {
                CheckUpdateResult = string.Format(T(NexusDashL.AboutUpdateFailed), exception.Message);
            }
            finally
            {
                _isCheckingUpdate = false;
            }
        }

        private static void OpenRepository()
        {
            var repositoryUrl = GetAssemblyMetadata("ProjectUrl", "https://codewf.com");
            Process.Start(new ProcessStartInfo(repositoryUrl)
            {
                UseShellExecute = true
            });
        }

        protected override void RaiseLocalizedProperties()
        {
            this.RaisePropertyChanged(nameof(Header));
            this.RaisePropertyChanged(nameof(CheckUpdateLabel));
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
