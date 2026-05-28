using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NexusDash.Services
{
    public enum ProcessSnapshotExportFormat
    {
        Json,
        Csv
    }

    public enum ProcessSnapshotExportScope
    {
        ProcessList,
        SelectedProcess
    }

    public sealed record ProcessSnapshotExportRow(
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

    public sealed record ProcessSnapshotExportState(
        int TotalProcessCount,
        string DialogTitle,
        IReadOnlyList<ProcessSnapshotExportRow> VisibleRows,
        ProcessSnapshotExportRow? SelectedRow);

    public sealed record ProcessSnapshotExportResult(
        bool Exported,
        int RowCount,
        int? SelectedProcessId,
        string FilePath);

    public interface IProcessSnapshotExportService
    {
        Task<ProcessSnapshotExportResult> ExportAsync(
            ProcessSnapshotExportState state,
            ProcessSnapshotExportFormat format,
            ProcessSnapshotExportScope scope);
    }

    public sealed class ProcessSnapshotExportService : IProcessSnapshotExportService
    {
        private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private sealed record ProcessSnapshot(
            DateTimeOffset CapturedAt,
            int TotalProcessCount,
            int ExportedProcessCount,
            IReadOnlyList<ProcessSnapshotExportRow> Processes);

        private sealed record SelectedProcessSnapshot(
            DateTimeOffset CapturedAt,
            ProcessSnapshotExportRow Process);

        public async Task<ProcessSnapshotExportResult> ExportAsync(
            ProcessSnapshotExportState state,
            ProcessSnapshotExportFormat format,
            ProcessSnapshotExportScope scope)
        {
            var rows = CreateSnapshotRows(state, scope);
            if (rows.Count == 0)
            {
                return new ProcessSnapshotExportResult(false, 0, null, "");
            }

            var topLevel = GetTopLevel();
            if (topLevel is null)
            {
                return new ProcessSnapshotExportResult(false, 0, null, "");
            }

            var extension = format == ProcessSnapshotExportFormat.Json ? "json" : "csv";
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = state.DialogTitle,
                SuggestedFileName = CreateSuggestedSnapshotFileName(scope, rows[0], extension),
                FileTypeChoices = new[]
                {
                    CreateSnapshotFileType(format)
                }
            });

            if (file is null)
            {
                return new ProcessSnapshotExportResult(false, 0, null, "");
            }

            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek)
            {
                stream.SetLength(0);
            }

            await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            await writer.WriteAsync(format == ProcessSnapshotExportFormat.Json
                ? CreateSnapshotJson(state, rows, scope)
                : CreateSnapshotCsv(rows));

            return new ProcessSnapshotExportResult(
                true,
                rows.Count,
                scope == ProcessSnapshotExportScope.SelectedProcess ? rows[0].Pid : null,
                GetStorageFilePath(file));
        }

        private static TopLevel? GetTopLevel()
        {
            return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
        }

        private static IReadOnlyList<ProcessSnapshotExportRow> CreateSnapshotRows(
            ProcessSnapshotExportState state,
            ProcessSnapshotExportScope scope)
        {
            return scope == ProcessSnapshotExportScope.SelectedProcess
                ? state.SelectedRow is null ? [] : [state.SelectedRow]
                : state.VisibleRows;
        }

        private static FilePickerFileType CreateSnapshotFileType(ProcessSnapshotExportFormat format)
        {
            return format == ProcessSnapshotExportFormat.Json
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

        private static string CreateSuggestedSnapshotFileName(
            ProcessSnapshotExportScope scope,
            ProcessSnapshotExportRow firstRow,
            string extension)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            return scope == ProcessSnapshotExportScope.SelectedProcess
                ? $"nexusdash-process-{firstRow.Pid}-{timestamp}.{extension}"
                : $"nexusdash-processes-{timestamp}.{extension}";
        }

        private static string CreateSnapshotJson(
            ProcessSnapshotExportState state,
            IReadOnlyList<ProcessSnapshotExportRow> rows,
            ProcessSnapshotExportScope scope)
        {
            if (scope == ProcessSnapshotExportScope.SelectedProcess)
            {
                var selectedSnapshot = new SelectedProcessSnapshot(DateTimeOffset.Now, rows[0]);
                return JsonSerializer.Serialize(selectedSnapshot, SnapshotJsonOptions);
            }

            var snapshot = new ProcessSnapshot(
                DateTimeOffset.Now,
                state.TotalProcessCount,
                rows.Count,
                rows);
            return JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
        }

        private static string CreateSnapshotCsv(IReadOnlyList<ProcessSnapshotExportRow> rows)
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
