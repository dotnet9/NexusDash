using System;
using System.Globalization;

namespace NexusDash.Models
{
    public sealed class ProcessNetworkConnection
    {
        public string Protocol { get; init; } = "";
        public int? Pid { get; init; }
        public string? ProcessName { get; init; }
        public string LocalAddress { get; init; } = "";
        public int LocalPort { get; init; }
        public string RemoteAddress { get; init; } = "";
        public int RemotePort { get; init; }
        public string State { get; init; } = "";
        public DateTime Timestamp { get; init; } = DateTime.Now;

        public string PidText => Pid?.ToString(CultureInfo.CurrentCulture) ?? "";

        public string ProcessNameText => string.IsNullOrWhiteSpace(ProcessName)
            ? ""
            : ProcessName;

        public string LocalEndpointText => FormatEndpoint(LocalAddress, LocalPort);

        public string RemoteEndpointText => FormatEndpoint(RemoteAddress, RemotePort);

        public string TimestampText => Timestamp.ToString("T", CultureInfo.CurrentCulture);

        private static string FormatEndpoint(string address, int port)
        {
            var normalizedAddress = string.IsNullOrWhiteSpace(address) ? "*" : address;
            if (port <= 0)
            {
                return normalizedAddress == "*" ? "*:*" : normalizedAddress;
            }

            if (normalizedAddress.Contains(':', StringComparison.Ordinal) &&
                !normalizedAddress.StartsWith("[", StringComparison.Ordinal))
            {
                return $"[{normalizedAddress}]:{port}";
            }

            return $"{normalizedAddress}:{port}";
        }
    }
}
