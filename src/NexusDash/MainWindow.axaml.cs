using AtomUI.Desktop.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using NexusDash.Models;
using NexusDash.Services;
using NexusDash.ViewModels;
using NexusDash.Views;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NexusDash
{
    public partial class MainWindow : AtomUI.Desktop.Controls.Window
    {
        private const double CompactTitleBarHeight = 40;
        private const string TitleBarTitleBindingPath = "DataContext.AppNameText";
        private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private enum SnapshotExportFormat
        {
            Json,
            Csv
        }

        private sealed record ProcessSnapshot(
            DateTimeOffset CapturedAt,
            int TotalProcessCount,
            int ExportedProcessCount,
            IReadOnlyList<ProcessSnapshotRow> Processes);

        private sealed record ProcessSnapshotRow(
            int Pid,
            int? ParentPid,
            string Name,
            string RawName,
            string? Publisher,
            string Category,
            double CpuPercent,
            ulong WorkingSetBytes,
            double DiskBytesPerSecond,
            int TcpConnectionCount,
            int UdpConnectionCount,
            int NetworkConnectionCount,
            double? GpuPercent,
            string? ExecutablePath,
            string? CommandLine,
            DateTime? StartTime,
            bool IsAccessDenied);

        private static readonly (double Width, double Height)[] SupersededDefaultWindowSizes =
        [
            (1280, 820),
            (1440, 820),
            (1440, 900)
        ];

        private TitleBarSearchAddOn? _titleBarSearchAddOn;
        private IDisposable? _titleBarTitleBinding;
        private IUserPreferencesService? _userPreferencesService;

        public MainWindow()
        {
            // Avalonia 运行时资源加载器需要公开无参构造；真实应用入口由 Prism 注入下方构造器。
            InitializeComponent();
        }

        public MainWindow(IUserPreferencesService userPreferencesService)
        {
            _userPreferencesService = userPreferencesService;
            InitializeComponent();
            ApplyWindowPreferences();
            AddHandler(PointerPressedEvent, HandleTitleBarDragPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            DataContextChanged += (_, _) => ApplyTitleBarDataContext();
            ApplyTitleBarDataContext();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            SaveWindowPreferences();
            _titleBarTitleBinding?.Dispose();
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }

            base.OnClosing(e);
        }

        protected override WindowTitleBar? NotifyCreateTitleBar(WindowTitleBar? oldTitleBar)
        {
            return oldTitleBar ?? new WindowTitleBar
            {
                Name = "PART_TitleBar",
                Height = CompactTitleBarHeight,
                MinHeight = CompactTitleBarHeight,
                MaxHeight = CompactTitleBarHeight,
                Padding = new Thickness(8, 0),
                FontSize = 12
            };
        }

        protected override void NotifyConfigureTitleBar(WindowTitleBar titleBar)
        {
            base.NotifyConfigureTitleBar(titleBar);
            _titleBarSearchAddOn = new TitleBarSearchAddOn();
            ApplyTitleBarDataContext();
            _titleBarTitleBinding?.Dispose();
            _titleBarTitleBinding = titleBar.Bind(
                WindowTitleBar.TitleProperty,
                new Binding(TitleBarTitleBindingPath)
                {
                    Source = this
                });
            titleBar.SetCurrentValue(WindowTitleBar.LeftAddOnProperty, null);
            titleBar.SetCurrentValue(WindowTitleBar.RightAddOnProperty, _titleBarSearchAddOn);
        }

        private void ApplyTitleBarDataContext()
        {
            if (_titleBarSearchAddOn is not null)
            {
                _titleBarSearchAddOn.DataContext = DataContext;
            }
        }

        private void ApplyWindowPreferences()
        {
            if (_userPreferencesService is null)
            {
                return;
            }

            var preferences = _userPreferencesService.Load();
            // 旧版本会把默认尺寸写进偏好；这些值不是用户主动调整，允许跟随新版窗口基准。
            if (IsSupersededDefaultWindowSize(preferences.WindowWidth, preferences.WindowHeight))
            {
                return;
            }

            if (preferences.WindowWidth >= MinWidth)
            {
                Width = preferences.WindowWidth;
            }

            if (preferences.WindowHeight >= MinHeight)
            {
                Height = preferences.WindowHeight;
            }
        }

        private void SaveWindowPreferences()
        {
            if (WindowState != WindowState.Normal || _userPreferencesService is null)
            {
                return;
            }

            _userPreferencesService.Update(preferences =>
            {
                preferences.WindowWidth = Math.Max(Width, MinWidth);
                preferences.WindowHeight = Math.Max(Height, MinHeight);
            });
        }

        private static bool IsSupersededDefaultWindowSize(double width, double height)
        {
            return SupersededDefaultWindowSizes.Any(size =>
                size.Width.Equals(width) &&
                size.Height.Equals(height));
        }

        private void HandleTitleBarDragPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
                WindowState == WindowState.FullScreen ||
                !IsTitleBarDragSource(e))
            {
                return;
            }

            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                e.Handled = true;
                return;
            }

            BeginMoveDrag(e);
            e.Handled = true;
        }

        private bool IsTitleBarDragSource(PointerPressedEventArgs e)
        {
            var point = e.GetPosition(this);
            if (point.Y > CompactTitleBarHeight)
            {
                return false;
            }

            if (e.Source is not Visual source)
            {
                return true;
            }

            for (var current = source; current is not null; current = current.GetVisualParent())
            {
                var typeName = current.GetType().Name;
                if (current is Avalonia.Controls.TextBox ||
                    current is Avalonia.Controls.MenuItem ||
                    typeName.Contains("Button", StringComparison.Ordinal) ||
                    typeName.Contains("MenuItem", StringComparison.Ordinal) ||
                    typeName.Contains("CaptionButton", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private void ExportSnapshotButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control control)
            {
                return;
            }

            var viewModel = DataContext as MainWindowViewModel;
            var menu = new AtomUI.Desktop.Controls.MenuFlyout
            {
                Placement = PlacementMode.Top,
                IsMotionEnabled = true
            };

            AddExportMenuItem(menu, viewModel?.ExportJsonText ?? "Export JSON", SnapshotExportFormat.Json);
            AddExportMenuItem(menu, viewModel?.ExportCsvText ?? "Export CSV", SnapshotExportFormat.Csv);
            menu.ShowAt(control);
        }

        private void AddExportMenuItem(
            AtomUI.Desktop.Controls.MenuFlyout menu,
            string header,
            SnapshotExportFormat format)
        {
            var item = new AtomUI.Desktop.Controls.MenuItem
            {
                Header = header
            };
            item.Click += async (_, _) => await ExportProcessSnapshotAsync(format);
            menu.Items.Add(item);
        }

        private async Task ExportProcessSnapshotAsync(SnapshotExportFormat format)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            try
            {
                var rows = viewModel.VisibleProcesses
                    .Where(row => row.IsProcessRow)
                    .Select(CreateSnapshotRow)
                    .ToArray();
                var extension = format == SnapshotExportFormat.Json ? "json" : "csv";
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = viewModel.ExportSnapshotText,
                    SuggestedFileName = $"nexusdash-processes-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}",
                    FileTypeChoices = new[]
                    {
                        CreateSnapshotFileType(format)
                    }
                });

                if (file is null)
                {
                    return;
                }

                await using var stream = await file.OpenWriteAsync();
                if (stream.CanSeek)
                {
                    stream.SetLength(0);
                }

                await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                await writer.WriteAsync(format == SnapshotExportFormat.Json
                    ? CreateSnapshotJson(viewModel, rows)
                    : CreateSnapshotCsv(rows));

                viewModel.StatusMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    viewModel.StatusSnapshotExportedText,
                    rows.Length);
            }
            catch (Exception exception)
            {
                viewModel.StatusMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    viewModel.StatusSnapshotExportFailedText,
                    exception.Message);
            }
        }

        private static FilePickerFileType CreateSnapshotFileType(SnapshotExportFormat format)
        {
            return format == SnapshotExportFormat.Json
                ? new FilePickerFileType("JSON")
                {
                    Patterns = new[] { "*.json" },
                    MimeTypes = new[] { "application/json" }
                }
                : new FilePickerFileType("CSV")
                {
                    Patterns = new[] { "*.csv" },
                    MimeTypes = new[] { "text/csv" }
                };
        }

        private static string CreateSnapshotJson(MainWindowViewModel viewModel, IReadOnlyList<ProcessSnapshotRow> rows)
        {
            var snapshot = new ProcessSnapshot(
                DateTimeOffset.Now,
                viewModel.ProcessTotalCount,
                rows.Count,
                rows);
            return JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
        }

        private static string CreateSnapshotCsv(IReadOnlyList<ProcessSnapshotRow> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine("pid,parentPid,name,rawName,publisher,category,cpuPercent,workingSetBytes,diskBytesPerSecond,tcpConnectionCount,udpConnectionCount,networkConnectionCount,gpuPercent,executablePath,commandLine,startTime,isAccessDenied");
            foreach (var row in rows)
            {
                AppendCsvField(builder, row.Pid);
                AppendCsvField(builder, row.ParentPid);
                AppendCsvField(builder, row.Name);
                AppendCsvField(builder, row.RawName);
                AppendCsvField(builder, row.Publisher);
                AppendCsvField(builder, row.Category);
                AppendCsvField(builder, row.CpuPercent);
                AppendCsvField(builder, row.WorkingSetBytes);
                AppendCsvField(builder, row.DiskBytesPerSecond);
                AppendCsvField(builder, row.TcpConnectionCount);
                AppendCsvField(builder, row.UdpConnectionCount);
                AppendCsvField(builder, row.NetworkConnectionCount);
                AppendCsvField(builder, row.GpuPercent);
                AppendCsvField(builder, row.ExecutablePath);
                AppendCsvField(builder, row.CommandLine);
                AppendCsvField(builder, row.StartTime?.ToString("O", CultureInfo.InvariantCulture));
                AppendCsvField(builder, row.IsAccessDenied, isLast: true);
            }

            return builder.ToString();
        }

        private static ProcessSnapshotRow CreateSnapshotRow(ProcessRowViewModel row)
        {
            return new ProcessSnapshotRow(
                row.Pid,
                row.ParentPid,
                row.Name,
                row.RawName,
                row.Publisher,
                row.Category.ToString(),
                row.CpuPercent,
                row.WorkingSetBytes,
                row.DiskBytesPerSecond,
                row.TcpConnectionCount,
                row.UdpConnectionCount,
                row.NetworkConnectionCount,
                row.GpuPercent,
                row.ExecutablePath,
                row.CommandLine,
                row.StartTime,
                row.IsAccessDenied);
        }

        private static void AppendCsvField(StringBuilder builder, object? value, bool isLast = false)
        {
            var text = value switch
            {
                null => "",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? ""
            };

            if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
            {
                builder.Append('"');
                builder.Append(text.Replace("\"", "\"\""));
                builder.Append('"');
            }
            else
            {
                builder.Append(text);
            }

            builder.Append(isLast ? Environment.NewLine : ',');
        }

        private void NetworkConnectionsGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not DataGrid dataGrid ||
                !e.GetCurrentPoint(dataGrid).Properties.IsRightButtonPressed ||
                e.Source is not Visual source)
            {
                return;
            }

            var row = source.FindAncestorOfType<DataGridRow>(includeSelf: true);
            if (row?.DataContext is not ProcessNetworkConnection connection)
            {
                return;
            }

            e.Handled = true;
            ShowNetworkConnectionMenu(dataGrid, connection);
        }

        private void ShowNetworkConnectionMenu(DataGrid dataGrid, ProcessNetworkConnection connection)
        {
            var viewModel = DataContext as MainWindowViewModel;
            var menu = new AtomUI.Desktop.Controls.MenuFlyout
            {
                Placement = PlacementMode.Pointer,
                IsMotionEnabled = true
            };

            AddCopyMenuItem(
                menu,
                viewModel?.CopyLocalEndpointText ?? "Copy local endpoint",
                connection.LocalEndpointText);
            AddCopyMenuItem(
                menu,
                viewModel?.CopyRemoteEndpointText ?? "Copy remote endpoint",
                connection.RemoteEndpointText);
            AddCopyMenuItem(
                menu,
                viewModel?.CopyConnectionInfoText ?? "Copy connection row",
                FormatConnectionInfo(connection));

            menu.ShowAt(dataGrid);
        }

        private void AddCopyMenuItem(AtomUI.Desktop.Controls.MenuFlyout menu, string header, string text)
        {
            var item = new AtomUI.Desktop.Controls.MenuItem
            {
                Header = header
            };
            item.Click += async (_, _) => await CopyTextAsync(text);
            menu.Items.Add(item);
        }

        private async Task CopyTextAsync(string text)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(text);
            }
        }

        private static string FormatConnectionInfo(ProcessNetworkConnection connection)
        {
            return string.Join(
                "\t",
                connection.Protocol,
                connection.LocalEndpointText,
                connection.RemoteEndpointText,
                connection.State,
                connection.TimestampText);
        }
    }
}
