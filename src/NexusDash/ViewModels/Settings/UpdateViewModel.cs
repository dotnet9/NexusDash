using System;
using System.Diagnostics;
using System.Globalization;
using Prism.Commands;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Core.UpdateChecking;
using ReactiveUI;

namespace NexusDash.ViewModels.Settings;

/// <summary>应用维护更新界面状态，检查、下载、校验和安装交给 Toolkit。</summary>
public sealed class UpdateViewModel : ReactiveObject, IDisposable
{
    private readonly IUpdateService _service;
    private readonly bool _ownsService;
    private readonly Action<string> _openUrl;
    private readonly Action _exit;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _operation;
    private UpdateInfo? _update;
    private UpdateDownloadResult? _download;
    private string? _error;
    private double? _progress;
    private bool _disposed;
    public UpdateViewModel(IUpdateService? service = null, Action<string>? openUrl = null, Action? exit = null)
    {
        _service = service ?? new UpdateService(new UpdateServiceOptions { Owner = "dotnet9", Repository = "NexusDash", ProductName = "NexusDash" });
        _ownsService = service is null;
        _openUrl = openUrl ?? (url => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }));
        _exit = exit ?? (() => (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown());
        ActionCommand = new DelegateCommand(async () => await AdvanceAsync(), () => !IsBusy && !_disposed);
        CheckCommand = new DelegateCommand(async () => await CheckAsync(), () => !IsBusy && !_disposed);
        CancelCommand = new DelegateCommand(() => _operation?.Cancel(), () => IsBusy && !_disposed);
        I18nManager.Instance.CultureChanged += OnCultureChanged;
    }
    public DelegateCommand ActionCommand { get; }
    public DelegateCommand CheckCommand { get; }
    public DelegateCommand CancelCommand { get; }
    public UpdateState State { get; private set; }
    public bool IsBusy => State is UpdateState.Checking or UpdateState.Downloading or UpdateState.Installing;
    public bool IsDownloading => State == UpdateState.Downloading;
    public bool CanRecheck => State is UpdateState.Available or UpdateState.Downloaded;
    public bool IsIndeterminate => !_progress.HasValue;
    public double Progress => _progress ?? 0;
    private static string T(string key) => I18nManager.Instance.GetResource(key) ?? key;
    private bool CanDownload => !string.IsNullOrWhiteSpace(_update?.AssetUrl) && !string.IsNullOrWhiteSpace(_update.ChecksumUrl);
    public string ActionText => T(State switch
    {
        UpdateState.Checking => NexusDashL.AboutChecking,
        UpdateState.Downloading => NexusDashL.AboutDownloading,
        UpdateState.Installing => NexusDashL.AboutInstalling,
        UpdateState.Available when !CanDownload => NexusDashL.UpdateReleasePage,
        UpdateState.Available => NexusDashL.UpdateDownload,
        UpdateState.Downloaded when _download?.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true => NexusDashL.AboutOpenFolder,
        UpdateState.Downloaded => NexusDashL.UpdateInstall,
        _ => NexusDashL.UpdateCheck
    });
    public string StatusText => State switch
    {
        UpdateState.Checking => T(NexusDashL.AboutChecking),
        UpdateState.Current => T(NexusDashL.AboutCurrent),
        UpdateState.Available => string.Format(CultureInfo.CurrentCulture, T(NexusDashL.AboutAvailable), _update?.Tag),
        UpdateState.Downloading => T(NexusDashL.AboutDownloading),
        UpdateState.Downloaded => T(NexusDashL.AboutDownloaded),
        UpdateState.Failed => string.Format(CultureInfo.CurrentCulture, T(NexusDashL.AboutFailed), _error),
        UpdateState.Canceled => T(NexusDashL.AboutCanceled),
        UpdateState.Installed => _error ?? string.Empty,
        _ => string.Empty
    };
    public Task CheckAsync() => RunAsync(UpdateState.Checking, async token =>
    {
        _update = null; _download = null; _error = null;
        var result = await _service.CheckAsync(UpdateVersion.Current(typeof(App).Assembly), token);
        token.ThrowIfCancellationRequested();
        _update = result.Update; _error = result.Error;
        SetState(!result.Succeeded ? UpdateState.Failed : result.Update is not null ? UpdateState.Available : UpdateState.Current);
    });
    public async Task AdvanceAsync()
    {
        if (_disposed || IsBusy) return;
        if (State == UpdateState.Available && !CanDownload)
        {
            try { _openUrl(_update!.PageUrl); }
            catch (Exception error) { _error = error.Message; SetState(UpdateState.Failed); }
            return;
        }
        if (State == UpdateState.Available)
        {
            await RunAsync(UpdateState.Downloading, async token =>
            {
                _progress = null;
                _download = await _service.DownloadAsync(_update!, new Progress<UpdateDownloadProgress>(progress =>
                {
                    if (_disposed || !IsDownloading) return;
                    _progress = progress.Percentage; Refresh();
                }), token);
                token.ThrowIfCancellationRequested();
                SetState(UpdateState.Downloaded);
            });
        }
        else if (State == UpdateState.Downloaded)
        {
            await RunAsync(UpdateState.Installing, async token =>
            {
                var result = await _service.InstallAsync(_download!, token);
                _error = result.Message; SetState(UpdateState.Installed);
                if (!_disposed && result.ShouldExitApplication) _exit();
            });
        }
        else await CheckAsync();
    }
    private async Task RunAsync(UpdateState state, Func<CancellationToken, Task> work)
    {
        if (_disposed || IsBusy) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _operation = operation; SetState(state);
        try { await work(operation.Token); }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { SetState(UpdateState.Canceled); }
        catch (Exception error) { _error = error.Message; SetState(UpdateState.Failed); }
        finally { _operation = null; }
    }
    private void SetState(UpdateState state)
    {
        if (_disposed) return;
        State = state;
        ActionCommand.RaiseCanExecuteChanged();
        CheckCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        Refresh();
    }
    private void OnCultureChanged(object? sender, EventArgs args)
    {
        if (global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) Refresh();
        else global::Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);
    }
    private void Refresh()
    {
        if (_disposed) return;
        foreach (var property in new[] { nameof(State), nameof(IsBusy), nameof(IsDownloading), nameof(CanRecheck), nameof(IsIndeterminate), nameof(Progress), nameof(ActionText), nameof(StatusText) }) this.RaisePropertyChanged(property);
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _lifetime.Cancel();
        I18nManager.Instance.CultureChanged -= OnCultureChanged;
        ActionCommand.RaiseCanExecuteChanged(); CheckCommand.RaiseCanExecuteChanged(); CancelCommand.RaiseCanExecuteChanged();
        _lifetime.Dispose();
        if (_ownsService) (_service as IDisposable)?.Dispose();
    }
}

public enum UpdateState { Idle, Checking, Current, Available, Downloading, Downloaded, Installing, Installed, Failed, Canceled }
