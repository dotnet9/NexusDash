using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace NexusDash.ViewModels
{
    public static class ProcessTableSort
    {
        public static string NormalizeColumnKey(string? columnKey)
        {
            return TryNormalizeColumnKey(columnKey, out var normalizedKey)
                ? normalizedKey
                : ProcessTableColumns.Name;
        }

        public static bool TryNormalizeColumnKey(string? columnKey, out string normalizedKey)
        {
            normalizedKey = columnKey switch
            {
                ProcessTableColumns.Pid => ProcessTableColumns.Pid,
                ProcessTableColumns.ParentPid => ProcessTableColumns.ParentPid,
                ProcessTableColumns.Name => ProcessTableColumns.Name,
                ProcessTableColumns.Publisher => ProcessTableColumns.Publisher,
                ProcessTableColumns.Cpu => ProcessTableColumns.Cpu,
                ProcessTableColumns.Memory => ProcessTableColumns.Memory,
                ProcessTableColumns.Disk => ProcessTableColumns.Disk,
                ProcessTableColumns.Network => ProcessTableColumns.Network,
                ProcessTableColumns.Gpu => ProcessTableColumns.Gpu,
                _ => ""
            };

            return normalizedKey.Length > 0;
        }

        public static ListSortDirection GetDefaultDirection(string columnKey)
        {
            return columnKey is ProcessTableColumns.Cpu or
                   ProcessTableColumns.Memory or
                   ProcessTableColumns.Disk or
                   ProcessTableColumns.Network or
                   ProcessTableColumns.Gpu
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }

        public static ListSortDirection ToggleDirection(ListSortDirection direction)
        {
            return direction == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }

        public sealed class RowComparer(
            string columnKey,
            ListSortDirection direction) : IComparer<ProcessRowViewModel>
        {
            public int Compare(ProcessRowViewModel? x, ProcessRowViewModel? y)
            {
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                if (x is null)
                {
                    return 1;
                }

                if (y is null)
                {
                    return -1;
                }

                var result = columnKey switch
                {
                    ProcessTableColumns.Pid => CompareValue(x.Pid, y.Pid),
                    ProcessTableColumns.ParentPid => CompareNullableValue(x.ParentPid, y.ParentPid),
                    ProcessTableColumns.Publisher => CompareOptionalText(x.Publisher, y.Publisher),
                    ProcessTableColumns.Cpu => CompareMetric(x.CpuPercent, y.CpuPercent),
                    ProcessTableColumns.Memory => CompareValue(x.WorkingSetBytes, y.WorkingSetBytes),
                    ProcessTableColumns.Disk => CompareMetric(x.DiskBytesPerSecond, y.DiskBytesPerSecond),
                    ProcessTableColumns.Network => CompareNetwork(x, y),
                    ProcessTableColumns.Gpu => CompareNullableMetric(x.GpuPercent, y.GpuPercent),
                    _ => CompareText(x.Name, y.Name)
                };
                if (result != 0)
                {
                    return result;
                }

                result = CompareNaturalText(x.Name, y.Name);
                return result != 0 ? result : x.Pid.CompareTo(y.Pid);
            }

            private int CompareNetwork(ProcessRowViewModel x, ProcessRowViewModel y)
            {
                var result = CompareValue(x.NetworkConnectionCount, y.NetworkConnectionCount);
                if (result != 0)
                {
                    return result;
                }

                result = CompareValue(x.TcpConnectionCount, y.TcpConnectionCount);
                return result != 0 ? result : CompareValue(x.UdpConnectionCount, y.UdpConnectionCount);
            }

            private int CompareOptionalText(string? x, string? y)
            {
                var xMissing = string.IsNullOrWhiteSpace(x);
                var yMissing = string.IsNullOrWhiteSpace(y);
                if (xMissing != yMissing)
                {
                    return xMissing ? 1 : -1;
                }

                return xMissing ? 0 : CompareText(x!, y!);
            }

            private int CompareText(string x, string y)
            {
                return ApplyDirection(CompareNaturalText(x, y));
            }

            private int CompareNullableMetric(double? x, double? y)
            {
                var xHasValue = x is { } xValue && double.IsFinite(xValue);
                var yHasValue = y is { } yValue && double.IsFinite(yValue);
                if (xHasValue != yHasValue)
                {
                    return xHasValue ? -1 : 1;
                }

                return xHasValue ? CompareMetric(x!.Value, y!.Value) : 0;
            }

            private int CompareMetric(double x, double y)
            {
                var xValue = double.IsFinite(x) ? x : 0;
                var yValue = double.IsFinite(y) ? y : 0;
                return CompareValue(xValue, yValue);
            }

            private int CompareNullableValue<T>(T? x, T? y)
                where T : struct, IComparable<T>
            {
                if (x.HasValue != y.HasValue)
                {
                    return x.HasValue ? -1 : 1;
                }

                return x.HasValue ? CompareValue(x.Value, y!.Value) : 0;
            }

            private int CompareValue<T>(T x, T y)
                where T : IComparable<T>
            {
                return ApplyDirection(x.CompareTo(y));
            }

            private int ApplyDirection(int result)
            {
                return direction == ListSortDirection.Descending ? -result : result;
            }

            private static int CompareNaturalText(string? x, string? y)
            {
                x ??= "";
                y ??= "";

                var xIndex = 0;
                var yIndex = 0;
                while (xIndex < x.Length && yIndex < y.Length)
                {
                    var xIsDigit = IsAsciiDigit(x[xIndex]);
                    var yIsDigit = IsAsciiDigit(y[yIndex]);
                    var xStart = xIndex;
                    var yStart = yIndex;

                    while (xIndex < x.Length && IsAsciiDigit(x[xIndex]) == xIsDigit)
                    {
                        xIndex++;
                    }

                    while (yIndex < y.Length && IsAsciiDigit(y[yIndex]) == yIsDigit)
                    {
                        yIndex++;
                    }

                    var result = xIsDigit && yIsDigit
                        ? CompareNumberSegments(x, xStart, xIndex, y, yStart, yIndex)
                        : CompareTextSegments(x, xStart, xIndex, y, yStart, yIndex);
                    if (result != 0)
                    {
                        return result;
                    }
                }

                return (x.Length - xIndex).CompareTo(y.Length - yIndex);
            }

            private static int CompareNumberSegments(
                string x,
                int xStart,
                int xEnd,
                string y,
                int yStart,
                int yEnd)
            {
                var xValueStart = SkipLeadingZeroes(x, xStart, xEnd);
                var yValueStart = SkipLeadingZeroes(y, yStart, yEnd);
                var xValueLength = xEnd - xValueStart;
                var yValueLength = yEnd - yValueStart;
                if (xValueLength != yValueLength)
                {
                    return xValueLength.CompareTo(yValueLength);
                }

                for (var offset = 0; offset < xValueLength; offset++)
                {
                    var result = x[xValueStart + offset].CompareTo(y[yValueStart + offset]);
                    if (result != 0)
                    {
                        return result;
                    }
                }

                return (xEnd - xStart).CompareTo(yEnd - yStart);
            }

            private static int CompareTextSegments(
                string x,
                int xStart,
                int xEnd,
                string y,
                int yStart,
                int yEnd)
            {
                var xSegment = x[xStart..xEnd];
                var ySegment = y[yStart..yEnd];
                var result = CultureInfo.CurrentCulture.CompareInfo.Compare(
                    xSegment,
                    ySegment,
                    CompareOptions.IgnoreCase | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth);
                return result != 0 ? result : string.Compare(xSegment, ySegment, StringComparison.Ordinal);
            }

            private static int SkipLeadingZeroes(string value, int start, int end)
            {
                while (start < end && value[start] == '0')
                {
                    start++;
                }

                return start;
            }

            private static bool IsAsciiDigit(char value)
            {
                return value is >= '0' and <= '9';
            }
        }
    }
}
