using CodeWF.EventBus;
using System.Collections.Generic;

namespace NexusDash.ViewModels
{
    public sealed class ProcessOverviewViewModel : EventBusViewModel
    {
        private string _cpuText = "";
        private string _memoryText = "";
        private string _diskText = "";
        private string _networkText = "";
        private string _cpuUsageText = "";
        private string _memoryUsageText = "";
        private string _memorySummaryText = "";
        private string _diskSpeedText = "";
        private string _networkSpeedText = "";
        private string _topCpuProcessText = "";
        private string _topMemoryProcessText = "";
        private string _topDiskProcessText = "";
        private string _topNetworkProcessText = "";
        private double _cpuUsage;
        private IReadOnlyList<double> _cpuHistory = [];
        private IReadOnlyList<double> _memoryHistory = [];
        private IReadOnlyList<double> _diskHistory = [];
        private IReadOnlyList<double> _networkHistory = [];

        public ProcessOverviewViewModel(IEventBus eventBus)
            : base(eventBus)
        {
        }

        public string CpuText { get => _cpuText; private set => SetField(ref _cpuText, value, nameof(CpuText)); }
        public string MemoryText { get => _memoryText; private set => SetField(ref _memoryText, value, nameof(MemoryText)); }
        public string DiskText { get => _diskText; private set => SetField(ref _diskText, value, nameof(DiskText)); }
        public string NetworkText { get => _networkText; private set => SetField(ref _networkText, value, nameof(NetworkText)); }
        public string CpuUsageText { get => _cpuUsageText; private set => SetField(ref _cpuUsageText, value, nameof(CpuUsageText)); }
        public string MemoryUsageText { get => _memoryUsageText; private set => SetField(ref _memoryUsageText, value, nameof(MemoryUsageText)); }
        public string MemorySummaryText { get => _memorySummaryText; private set => SetField(ref _memorySummaryText, value, nameof(MemorySummaryText)); }
        public string DiskSpeedText { get => _diskSpeedText; private set => SetField(ref _diskSpeedText, value, nameof(DiskSpeedText)); }
        public string NetworkSpeedText { get => _networkSpeedText; private set => SetField(ref _networkSpeedText, value, nameof(NetworkSpeedText)); }
        public string TopCpuProcessText { get => _topCpuProcessText; private set => SetField(ref _topCpuProcessText, value, nameof(TopCpuProcessText)); }
        public string TopMemoryProcessText { get => _topMemoryProcessText; private set => SetField(ref _topMemoryProcessText, value, nameof(TopMemoryProcessText)); }
        public string TopDiskProcessText { get => _topDiskProcessText; private set => SetField(ref _topDiskProcessText, value, nameof(TopDiskProcessText)); }
        public string TopNetworkProcessText { get => _topNetworkProcessText; private set => SetField(ref _topNetworkProcessText, value, nameof(TopNetworkProcessText)); }
        public double CpuUsage { get => _cpuUsage; private set => SetField(ref _cpuUsage, value, nameof(CpuUsage)); }
        public IReadOnlyList<double> CpuHistory { get => _cpuHistory; private set => SetField(ref _cpuHistory, value, nameof(CpuHistory)); }
        public IReadOnlyList<double> MemoryHistory { get => _memoryHistory; private set => SetField(ref _memoryHistory, value, nameof(MemoryHistory)); }
        public IReadOnlyList<double> DiskHistory { get => _diskHistory; private set => SetField(ref _diskHistory, value, nameof(DiskHistory)); }
        public IReadOnlyList<double> NetworkHistory { get => _networkHistory; private set => SetField(ref _networkHistory, value, nameof(NetworkHistory)); }

        [EventHandler]
        private void ApplyState(ProcessOverviewStateChangedCommand command)
        {
            var state = command.State;
            CpuText = state.CpuText;
            MemoryText = state.MemoryText;
            DiskText = state.DiskText;
            NetworkText = state.NetworkText;
            CpuUsageText = state.CpuUsageText;
            MemoryUsageText = state.MemoryUsageText;
            MemorySummaryText = state.MemorySummaryText;
            DiskSpeedText = state.DiskSpeedText;
            NetworkSpeedText = state.NetworkSpeedText;
            TopCpuProcessText = state.TopCpuProcessText;
            TopMemoryProcessText = state.TopMemoryProcessText;
            TopDiskProcessText = state.TopDiskProcessText;
            TopNetworkProcessText = state.TopNetworkProcessText;
            CpuUsage = state.CpuUsage;
            CpuHistory = state.CpuHistory;
            MemoryHistory = state.MemoryHistory;
            DiskHistory = state.DiskHistory;
            NetworkHistory = state.NetworkHistory;
        }
    }
}
