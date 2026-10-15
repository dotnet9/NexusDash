using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Controls.Themes;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Core.Extensions;
using CodeWF.Toolkit.Core.UpdateChecking;
using CodeWF.Toolkit.EventBus;
using CodeWF.Toolkit.Logging;
using NexusDash;
using NexusDash.Models;
using NexusDash.Services;
using NexusDash.ViewModels;
using NexusDash.ViewModels.Settings;
using NexusDash.Views;
using NexusDash.Views.Settings;
using Prism.Ioc;

var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/ui-verification");
Directory.CreateDirectory(output);
Logger.Initialize(new LoggerOptions { EnableConsole = false });
AppBuilder.Configure<VerificationApp>().UseSkia()
    .With(new FontManagerOptions { FontFallbacks = [new FontFallback { FontFamily = new FontFamily("Microsoft YaHei UI") }] })
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
var app = (VerificationApp)Application.Current!;
var window = app.Container.Resolve<MainWindow>();
var model = (MainWindowViewModel)window.DataContext!;
model.PauseRefresh();
window.Show(); Pump();
if (args.Contains("--minimum-only"))
{
    VerifyMinimumLayout();
    window.Close();
    Console.WriteLine("NexusDash minimum layout verification passed: " + output);
    return;
}
VerifyThirdPartyLocales();
Check(window.Bounds.Width == 1440 && window.Bounds.Height == 760, "Actual main window applies product dimensions");
Check(window.FontFamily.Equals((FontFamily)app.FindResource("DefaultFontFamily")!), "Actual main window applies font fallback");
Check(window.TitleBarHeight == 40 && window.CaptionButtonWidth == 34, "Compact title bar metrics");
var appearance = app.Container.Resolve<SettingsViewModel>().Pages.OfType<AppearanceSettingsViewModel>().Single();
var about = app.Container.Resolve<SettingsViewModel>().Pages.OfType<AboutSettingsViewModel>().Single();
Check(appearance.ThemeOptions.Count == 3 && appearance.PaletteOptions.Count == 8, "Independent three modes and eight palettes");
var titleSettings = window.GetVisualDescendants().OfType<TitleBarSearchAddOn>().Single();
var titleToggle = titleSettings.FindControl<Ursa.Controls.ThemeToggleButton>("TitleBarThemeToggle")!;
var titleSwatches = titleSettings.FindControl<ListBox>("TitleBarPalettes")!;
var titleLanguages = titleSettings.FindControl<ComboBox>("TitleBarLanguageSelector")!;
var titleButton = titleToggle.GetVisualDescendants().OfType<Button>().Single(item => item.Name == "PART_ThemeButton");
foreach (var palette in appearance.PaletteOptions)
{
    titleSwatches.SelectedItem = palette; appearance.ThemeMode = StudioThemeMode.System; Pump();
    foreach (var expected in new[] { StudioThemeMode.Light, StudioThemeMode.Dark, StudioThemeMode.System })
    {
        titleButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Pump();
        Check(appearance.ThemeMode == expected && App.ThemeController!.Mode == expected, "Actual Ursa title-bar button selects: " + expected);
        Check(VerificationApp.Preferences.Value.PaletteKey == palette.Key && App.ThemeController!.Palette == ThemeResourceService.ResolvePalette(palette.Key), "Title bar retains the selected palette: " + palette.Key);
    }
}
foreach(var width in new[] { 1180, 1440 })
foreach(var culture in new[] { "zh-CN", "zh-Hant", "en-US", "ja-JP" })
{
    window.Width = width; titleLanguages.IsDropDownOpen = true; Pump();
    Check(titleLanguages.IsDropDownOpen && titleLanguages.Items.Count == model.Languages.Count, "Language icon opens every supported culture");
    titleLanguages.SelectedItem = model.Languages.Single(option => option.CultureName == culture);
    titleLanguages.IsDropDownOpen = false; Pump();
    Check(I18nManager.Instance.Culture.Name == culture, "Title bar switches the global language: " + culture);
    foreach (var mode in new[] { StudioThemeMode.System, StudioThemeMode.Light, StudioThemeMode.Dark })
    {
        appearance.ThemeMode = mode; Pump();
        var key = mode switch { StudioThemeMode.Light => NexusDashL.LightTheme, StudioThemeMode.Dark => NexusDashL.DarkTheme, _ => NexusDashL.ThemeSystem };
        Check(ToolTip.GetTip(titleButton) is TextBlock tip && tip.Text == I18nManager.Instance.GetResource(key),
            "Existing Ursa tooltip follows the current culture and mode: " + culture + "/" + mode);
    }
    var left = (Control)window.LeftContent!;
    Check(left.TranslatePoint(new Point(left.Bounds.Width, 0), window)!.Value.X <= titleSettings.TranslatePoint(default, window)!.Value.X,
        "Title bar content does not overlap: " + width + "/" + culture);
}
window.Width = 1440; titleLanguages.SelectedItem = model.Languages.Single(option => option.CultureName == "zh-CN"); Pump();

Check(ThemeResourceService.ResolvePreferenceThemeKey("aquatic", true) == "light" && ThemeResourceService.ResolvePreferencePaletteKey(null, "aquatic") == "aquatic", "Legacy palette migration preserves mode");
Check(ThemeResourceService.ResolvePreferenceThemeKey("night-sky", false) == "dark", "Legacy night sky preserves dark mode");
foreach (var palette in appearance.PaletteOptions)
{
    appearance.SelectedPalette = palette;
    Check(VerificationApp.Preferences.Value.PaletteKey == palette.Key, "Palette preference: " + palette.Key);
    foreach (var dark in new[] { false, true })
    {
        appearance.SelectedTheme = appearance.ThemeOptions.Single(option => option.Key == (dark ? "dark" : "light"));
        Pump();
        Check(App.ThemeController!.Palette == ThemeResourceService.ResolvePalette(palette.Key), "Palette controller: " + palette.Key);
        Check(App.ThemeController.Mode == (dark ? StudioThemeMode.Dark : StudioThemeMode.Light), "Explicit mode: " + dark);
        Capture(window, $"nexusdash-{palette.Key}-{(dark ? "dark" : "light")}.png");
    }
}
appearance.SelectedTheme = appearance.ThemeOptions.Single(option => option.Key == "system");
Check(VerificationApp.Preferences.Value.ThemeKey == "system" && App.ThemeController!.Mode == StudioThemeMode.System, "System mode persists independently");
App.ThemeController!.Palette = StudioPalette.DotNet;
App.ThemeController.Mode = StudioThemeMode.Dark;
var settingsView = new SettingsView { DataContext = app.Container.Resolve<SettingsViewModel>() };
var settingsWindow = new Window { Width = 1080, Height = 680, Content = settingsView };
settingsWindow.Show();
var settings = (SettingsViewModel)settingsView.DataContext!;
foreach (var culture in new[] { "en-US", "zh-CN", "zh-Hant", "ja-JP" })
{
    var notifications = 0;
    System.ComponentModel.PropertyChangedEventHandler changed = (_, args) => { if (args.PropertyName == nameof(about.Header)) notifications++; };
    about.PropertyChanged += changed;
    EventBus.Default.Publish(new LanguageChangeRequestedCommand(culture));
    about.PropertyChanged -= changed;
    Check(I18nManager.Instance.Culture.Name == culture && notifications > 0, "Requested language stays active and updates existing settings pages: " + culture);
    settings.SelectedPage = appearance; Pump();
    Check(appearance.SelectedTheme?.Key == "system", "Language change preserves selected system mode");
    Check(appearance.SelectedTheme.DisplayName == I18nManager.Instance.GetResource(NexusDashL.ThemeSystem), "Selected mode refreshes its displayed translation");
    Check(appearance.PaletteOptions.All(option => !option.DisplayName.StartsWith("NexusDash.")), "Localized palette labels: " + culture);
    Capture(settingsWindow, $"appearance-{culture}.png");
    settings.SelectedPage = about; Pump();
    Check(about.Version == UpdateVersion.Display(typeof(App).Assembly), "Actual application version: " + culture);
    Check(about.CompileTime != "—" && about.Platform.Contains(".NET"), "Native-safe compilation and platform metadata");
    Check(!about.Header.StartsWith("NexusDash."), "About dynamically localized: " + culture);
    Capture(settingsWindow, $"about-{culture}.png");
}
VerifyMinimumLayout();
VerifyUpdates();
settingsWindow.Close(); window.Close();
Console.WriteLine("NexusDash UI verification passed: " + output);

void VerifyMinimumLayout()
{
    var minimumSettings = app.Container.Resolve<SettingsViewModel>();
    window.Width = 1180;
    window.Height = 620;
    foreach (var culture in new[] { "zh-CN", "zh-Hant", "en-US", "ja-JP" })
    {
        EventBus.Default.Publish(new LanguageChangeRequestedCommand(culture));
        foreach (var node in model.ToolTree.ToolMenuItems.ToArray())
        {
            model.ToolTree.SelectedToolNode = node;
            Pump();
            if (node.ToolKey == MainWindowViewModel.ProcessManagerToolKey)
            {
                var tabs = window.GetVisualDescendants().OfType<ProcessManagerView>().Single()
                    .GetVisualDescendants().OfType<TabControl>().Single();
                for (var index = 0; index < tabs.ItemCount; index++)
                {
                    tabs.SelectedIndex = index;
                    Capture(window, $"minimum-{culture}-{node.ToolKey}-{index}.png");
                    if (index == 0)
                    {
                        foreach (var plot in window.GetVisualDescendants().OfType<MetricPlotView>())
                        {
                            var grid = (Grid)plot.Parent!;
                            var following = grid.Children.Where(child => Grid.GetRow(child) > Grid.GetRow(plot)).ToArray();
                            var plotBottom = plot.TranslatePoint(new Point(0, plot.Bounds.Height), window)!.Value.Y;
                            var nextTop = following.Min(child => child.TranslatePoint(default, window)!.Value.Y);
                            Check(plotBottom <= nextTop + 1,
                                $"Minimum performance chart avoids summary overlap: {culture}; plot bottom={plotBottom}; summary top={nextTop}");
                        }
                    }
                    if (index == 1)
                    {
                        var list = window.GetVisualDescendants().OfType<ProcessListView>().Single();
                        var grid = list.FindControl<DataGrid>("ProcessGrid")!;
                        if (!grid.ItemsSource.Cast<object>().Any())
                        {
                            ((ProcessListViewModel)list.DataContext!).VisibleProcesses.Add(new ProcessRowViewModel(
                                new ProcessMetrics { Pid = 6408, Name = "Layout fixture", RawName = "Layout fixture" },
                                "—", (_, _) => { }));
                            Pump();
                        }
                        foreach (var filter in list.GetVisualDescendants().OfType<CheckBox>())
                        {
                            var start = filter.TranslatePoint(default, list)!.Value;
                            Check(start.X >= 0 && start.X + filter.Bounds.Width <= list.Bounds.Width + 1,
                                "Minimum process filters remain fully reachable: " + culture);
                        }
                        Check(Equals(grid.Columns[0].Tag, "name") && grid.Columns.All(column => !column.IsVisible || column.ActualWidth >= 64),
                            "Minimum process table preserves readable columns: " + culture);
                        grid.ScrollIntoView(grid.ItemsSource.Cast<object>().First(), grid.Columns[^1]);
                        Pump();
                        var lastHeader = grid.GetVisualDescendants().OfType<DataGridColumnHeader>()
                            .Single(header => Equals(header.Content, grid.Columns[^1].Header));
                        var lastStart = lastHeader.TranslatePoint(default, grid)!.Value;
                        Check(lastStart.X >= grid.Columns[0].ActualWidth - 1 && lastStart.X + lastHeader.Bounds.Width <= grid.Bounds.Width + 1,
                            $"Horizontal scrolling exposes the final process column: {culture}; start={lastStart.X}; width={lastHeader.Bounds.Width}; grid={grid.Bounds.Width}; frozen={grid.Columns[0].ActualWidth}");
                        Capture(window, $"minimum-{culture}-{node.ToolKey}-scrolled.png");
                    }
                }
            }
            else if (node.ToolKey == MainWindowViewModel.SettingsToolKey)
            {
                foreach (var page in minimumSettings.Pages)
                {
                    minimumSettings.SelectedPage = page;
                    Capture(window, $"minimum-{culture}-settings-{page.GetType().Name}.png");
                }
            }
            else Capture(window, $"minimum-{culture}-{node.ToolKey}.png");
        }
    }
}

void VerifyThirdPartyLocales()
{
    var editor = new TextBox { Text = "Locale verification" };
    var shell = new Window { Width = 360, Height = 120, Content = editor };
    try
    {
        shell.Show(); Pump();
        var menu = (MenuFlyout)editor.ContextFlyout!;
        foreach (var culture in new[] { "en-US", "zh-CN", "ja-JP", "zh-Hant", "en-US" })
        {
            I18nManager.Instance.Culture = CultureInfo.GetCultureInfo(culture); Pump();
            ResourceDictionary semi = culture switch
            {
                "en-US" => new Semi.Avalonia.Locale.en_us(),
                "ja-JP" => new Semi.Avalonia.Locale.ja_jp(),
                "zh-Hant" => new Semi.Avalonia.Locale.zh_tw(),
                _ => new Semi.Avalonia.Locale.zh_cn(),
            };
            ResourceDictionary ursa = culture is "en-US" or "ja-JP"
                ? new Ursa.Themes.Semi.Locale.en_us() : new Ursa.Themes.Semi.Locale.zh_cn();
            menu.ShowAt(editor); Pump();
            Check(Equals(((MenuItem)menu.Items[1]!).Header, semi["STRING_MENU_COPY"])
                && Equals(((MenuItem)menu.Items[2]!).Header, semi["STRING_MENU_PASTE"]),
                "Existing Semi editor menu follows global culture: " + culture);
            Check(Equals(app.Styles.OfType<Ursa.Themes.Semi.UrsaSemiTheme>().Single().Resources["STRING_MENU_DIALOG_CANCEL"],
                ursa["STRING_MENU_DIALOG_CANCEL"]), "Existing Ursa dialog follows global culture: " + culture);
            menu.Hide();
        }
        app.Resources["STRING_MENU_COPY"] = "Host copy";
        EventBus.Default.Publish(new LanguageChangeRequestedCommand("zh-CN")); Pump();
        menu.ShowAt(editor); Pump();
        Check(Equals(((MenuItem)menu.Items[1]!).Header, "Host copy"), "Language changes preserve host overrides.");
        menu.Hide();
    }
    finally
    {
        app.Resources.Remove("STRING_MENU_COPY");
        I18nManager.Instance.Culture = CultureInfo.GetCultureInfo("zh-CN");
        shell.Close();
    }
}
void Check(bool success, string scenario)
{
    if (!success) throw new InvalidOperationException(scenario);
    Console.WriteLine("PASS " + scenario);
}
void Pump()
{
    var timer = Stopwatch.StartNew();
    do { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Thread.Sleep(10); } while (timer.ElapsedMilliseconds < 180);
}
void Complete(Task task)
{
    var timer = Stopwatch.StartNew();
    while (!task.IsCompleted && timer.ElapsedMilliseconds < 10000) Pump();
    Check(task.IsCompleted, "Async operation completes");
    task.GetAwaiter().GetResult();
}
void Capture(Window target, string name)
{
    Pump();
    using var bitmap = target.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rendered frame");
    using var stream = File.Create(Path.Combine(output, name));
    bitmap.Save(stream);
}
void VerifyUpdates()
{
    var fake = new FakeUpdates();
    var urls = new List<string>();
    var exits = 0;
    using var update = new UpdateViewModel(fake, urls.Add, () => exits++);
    Complete(update.CheckAsync()); Check(update.State == UpdateState.Current, "Current release");
    fake.Result = UpdateCheckResult.Failed("offline");
    Complete(update.CheckAsync()); Check(update.StatusText.Contains("offline"), "Failure remains visible");
    fake.Result = new UpdateCheckResult(new UpdateInfo(new Version(9, 0), "v9.0", "NexusDash", "Notes", "https://example.test/release", "https://example.test/setup.exe", "setup.exe", null), true, null);
    Complete(update.CheckAsync()); Complete(update.AdvanceAsync());
    Check(urls.Count == 1 && fake.Downloads == 0, "Missing checksum falls back to release page");
    fake.Result = fake.Result with { Update = fake.Result.Update! with { ChecksumUrl = "https://example.test/setup.exe.sha256" } };
    Complete(update.CheckAsync()); Complete(update.AdvanceAsync());
    Check(update.State == UpdateState.Downloaded && fake.Downloads == 1, "Verified download enables install");
    Complete(update.AdvanceAsync()); Check(update.State == UpdateState.Installed && exits == 0, "Manual installation keeps application open");
    fake.DownloadError = new IOException("checksum mismatch");
    Complete(update.CheckAsync()); Complete(update.AdvanceAsync());
    Check(update.State == UpdateState.Failed && update.StatusText.Contains("checksum mismatch"), "Checksum failure prevents installation");
    fake.Delay = new TaskCompletionSource<UpdateCheckResult>();
    var pending = update.CheckAsync(); Pump();
    Check(update.IsBusy && !update.ActionCommand.CanExecute(), "Busy commands disabled");
    var checks = fake.Checks; Complete(update.CheckAsync());
    Check(fake.Checks == checks, "Duplicate check ignored");
    var before = update.ActionText;
    EventBus.Default.Publish(new LanguageChangeRequestedCommand("en-US")); Pump();
    Check(update.ActionText != before, "Pending update changes language");
    update.CancelCommand.Execute(); Complete(pending);
    Check(update.State == UpdateState.Canceled && !update.IsBusy, "Cancel restores commands");
    var closing = new UpdateViewModel(fake, urls.Add, () => exits++);
    var unfinished = closing.CheckAsync(); closing.Dispose(); Complete(unfinished);
    Check(urls.Count == 1 && exits == 0, "Closing cancels without starting external operations");
}

sealed class VerificationApp : App
{
    public static readonly MemoryPreferences Preferences = new();
    protected override IUserPreferencesService CreatePreferencesService() => Preferences;
}
sealed class MemoryPreferences : IUserPreferencesService
{
    public UserPreferences Value { get; } = new() { CultureName = "zh-CN", ThemeKey = "dark", PaletteKey = "dotnet" };
    public UserPreferences Load() => Value;
    public void Update(Action<UserPreferences> update) => update(Value);
}
sealed class FakeUpdates : IUpdateService
{
    public UpdateServiceOptions Options { get; } = new() { Repository = "NexusDash", ProductName = "NexusDash" };
    public Version CurrentVersion => new(1, 0);
    public string CurrentVersionText => "1.0";
    public UpdateCheckResult Result { get; set; } = UpdateCheckResult.Latest();
    public TaskCompletionSource<UpdateCheckResult>? Delay { get; set; }
    public Exception? DownloadError { get; set; }
    public int Checks { get; private set; }
    public int Downloads { get; private set; }
    public Task<UpdateCheckResult> CheckAsync(Version? current = null, CancellationToken cancellationToken = default)
    {
        Checks++;
        return Delay?.Task.WaitAsync(cancellationToken) ?? Task.FromResult(Result);
    }
    public Task<UpdateDownloadResult> DownloadAsync(UpdateInfo update, IProgress<UpdateDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Downloads++;
        if (DownloadError is not null) throw DownloadError;
        progress?.Report(new UpdateDownloadProgress(50, 100));
        return Task.FromResult(new UpdateDownloadResult("setup.exe", "setup.exe", 100));
    }
    public Task<UpdateInstallResult> InstallAsync(UpdateDownloadResult download, CancellationToken cancellationToken = default) => Task.FromResult(new UpdateInstallResult(UpdateInstallKind.Manual, "Manual installation") { ShouldExitApplication = false });
}
