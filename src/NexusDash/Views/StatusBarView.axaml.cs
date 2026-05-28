using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using CodeWF.Log.Core;
using NexusDash.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AtomMenuFlyout = AtomUI.Desktop.Controls.MenuFlyout;
using AtomMenuItem = AtomUI.Desktop.Controls.MenuItem;

namespace NexusDash.Views
{
    public partial class StatusBarView : UserControl
    {
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

        private enum SnapshotExportScope
        {
            ProcessList,
            SelectedProcess
        }

        private sealed record ProcessSnapshot(
            DateTimeOffset CapturedAt,
            int TotalProcessCount,
            int ExportedProcessCount,
            IReadOnlyList<ProcessSnapshotRow> Processes);

        private sealed record SelectedProcessSnapshot(
            DateTimeOffset CapturedAt,
            ProcessSnapshotRow Process);

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

        public StatusBarView()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void SettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control control ||
                DataContext is not StatusBarViewModel viewModel)
            {
                return;
            }

            var menu = new AtomMenuFlyout
            {
                Placement = PlacementMode.Top,
                IsMotionEnabled = true
            };

            var settingsItem = new AtomMenuItem
            {
                Header = viewModel.SettingsText
            };
            settingsItem.Click += (_, _) => viewModel.OpenSettingsWindowCommand.Execute();
            menu.Items.Add(settingsItem);

            var rememberWindowSizeItem = new AtomMenuItem
            {
                Header = viewModel.RememberWindowSizeText,
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = viewModel.RememberWindowSize
            };
            rememberWindowSizeItem.Click += (_, _) =>
                viewModel.SetRememberWindowSize(rememberWindowSizeItem.IsChecked);
            menu.Items.Add(rememberWindowSizeItem);

            menu.ShowAt(control);
        }

        private void ExportSnapshotButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control control)
            {
                return;
            }

            var viewModel = DataContext as StatusBarViewModel;
            var menu = new AtomMenuFlyout
            {
                Placement = PlacementMode.Top,
                IsMotionEnabled = true
            };

            AddExportMenuItem(
                menu,
                viewModel?.ExportProcessListJsonText ?? "Export process list JSON",
                SnapshotExportFormat.Json,
                SnapshotExportScope.ProcessList,
                isEnabled: true);
            AddExportMenuItem(
                menu,
                viewModel?.ExportProcessListCsvText ?? "Export process list CSV",
                SnapshotExportFormat.Csv,
                SnapshotExportScope.ProcessList,
                isEnabled: true);
            AddExportMenuItem(
                menu,
                viewModel?.ExportSelectedProcessJsonText ?? "Export selected process JSON",
                SnapshotExportFormat.Json,
                SnapshotExportScope.SelectedProcess,
                viewModel?.HasSelectedProcess == true);
            AddExportMenuItem(
                menu,
                viewModel?.ExportSelectedProcessCsvText ?? "Export selected process CSV",
                SnapshotExportFormat.Csv,
                SnapshotExportScope.SelectedProcess,
                viewModel?.HasSelectedProcess == true);
            menu.ShowAt(control);
        }

        private void AddExportMenuItem(
            AtomMenuFlyout menu,
            string header,
            SnapshotExportFormat format,
            SnapshotExportScope scope,
            bool isEnabled)
        {
            var item = new AtomMenuItem
            {
                Header = header,
                IsEnabled = isEnabled
            };
            item.Click += async (_, _) => await ExportProcessSnapshotAsync(format, scope);
            menu.Items.Add(item);
        }

        private async Task ExportProcessSnapshotAsync(SnapshotExportFormat format, SnapshotExportScope scope)
        {
            if (DataContext is not StatusBarViewModel viewModel)
            {
                return;
            }

            try
            {
                var rows = CreateSnapshotRows(viewModel, scope);
                if (rows.Count == 0)
                {
                    return;
                }

                var extension = format == SnapshotExportFormat.Json ? "json" : "csv";
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel is null)
                {
                    return;
                }

                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = viewModel.ExportSnapshotText,
                    SuggestedFileName = CreateSuggestedSnapshotFileName(scope, rows[0], extension),
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
                    ? CreateSnapshotJson(viewModel, rows, scope)
                    : CreateSnapshotCsv(rows));

                viewModel.ReportStatus(CreateSnapshotStatusMessage(viewModel, rows, scope));
                Logger.Info(
                    $"Exported process snapshot: scope={scope}, format={format}, rows={rows.Count}, path={GetStorageFilePath(file)}",
                    $"导出进程快照：{rows.Count} 条，{GetStorageFilePath(file)}",
                    log2Console: false);
            }
            catch (Exception exception)
            {
                var statusMessage = string.Format(
                    CultureInfo.CurrentCulture,
                    viewModel.StatusSnapshotExportFailedText,
                    exception.Message);
                viewModel.ReportStatus(statusMessage);
                Logger.Error(
                    "Process snapshot export failed.",
                    exception,
                    statusMessage,
                    log2Console: false);
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

        private static IReadOnlyList<ProcessSnapshotRow> CreateSnapshotRows(
            StatusBarViewModel viewModel,
            SnapshotExportScope scope)
        {
            if (scope == SnapshotExportScope.SelectedProcess)
            {
                return viewModel.SelectedProcess is null
                    ? []
                    : [CreateSnapshotRow(viewModel.SelectedProcess)];
            }

            return viewModel.VisibleProcesses
                .Where(row => row.IsProcessRow)
                .Select(CreateSnapshotRow)
                .ToArray();
        }

        private static string CreateSuggestedSnapshotFileName(
            SnapshotExportScope scope,
            ProcessSnapshotRow firstRow,
            string extension)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            return scope == SnapshotExportScope.SelectedProcess
                ? $"nexusdash-process-{firstRow.Pid}-{timestamp}.{extension}"
                : $"nexusdash-processes-{timestamp}.{extension}";
        }

        private static string CreateSnapshotJson(
            StatusBarViewModel viewModel,
            IReadOnlyList<ProcessSnapshotRow> rows,
            SnapshotExportScope scope)
        {
            if (scope == SnapshotExportScope.SelectedProcess)
            {
                var selectedSnapshot = new SelectedProcessSnapshot(DateTimeOffset.Now, rows[0]);
                return JsonSerializer.Serialize(selectedSnapshot, SnapshotJsonOptions);
            }

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

        private static string CreateSnapshotStatusMessage(
            StatusBarViewModel viewModel,
            IReadOnlyList<ProcessSnapshotRow> rows,
            SnapshotExportScope scope)
        {
            return scope == SnapshotExportScope.SelectedProcess
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    viewModel.StatusSelectedProcessSnapshotExportedText,
                    rows[0].Pid)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    viewModel.StatusSnapshotExportedText,
                    rows.Count);
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

        private static string GetStorageFilePath(IStorageFile file)
        {
            try
            {
                return file.Path.IsFile ? file.Path.LocalPath : file.Name;
            }
            catch
            {
                return file.Name;
            }
        }
    }
}
