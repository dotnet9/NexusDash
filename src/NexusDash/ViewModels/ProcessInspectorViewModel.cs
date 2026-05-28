using CodeWF.EventBus;
using NexusDash.Models;
using System.Collections.Generic;

namespace NexusDash.ViewModels
{
    public sealed class ProcessInspectorViewModel : EventBusViewModel
    {
        private string _detailsText = "";
        private string _handlesText = "";
        private string _networkText = "";
        private string _servicesText = "";
        private string _startupText = "";
        private string _noProcessSelectedText = "";
        private string _accessLimitedText = "";
        private string _accessLimitedDescriptionText = "";
        private string _pidText = "";
        private string _publisherText = "";
        private string _startTimeText = "";
        private string _cpuText = "";
        private string _memoryText = "";
        private string _pathText = "";
        private string _commandLineText = "";
        private string _handlesSearchPlaceholderText = "";
        private string _handlesUnavailableText = "";
        private string _servicesUnavailableText = "";
        private string _startupUnavailableText = "";
        private string _processNetworkConnectionsText = "";
        private string _selectedProcessNetworkSummaryText = "";
        private string _selectedProcessConnectionTotalText = "";
        private string _selectedProcessTcpConnectionCountText = "";
        private string _selectedProcessUdpConnectionCountText = "";
        private string _networkSelectProcessText = "";
        private string _networkNoConnectionsText = "";
        private string _protocolText = "";
        private string _localEndpointText = "";
        private string _remoteEndpointText = "";
        private string _stateText = "";
        private string _lastSeenText = "";
        private string _copyLocalEndpointText = "";
        private string _copyRemoteEndpointText = "";
        private string _copyConnectionInfoText = "";
        private ProcessRowViewModel? _selectedProcess;
        private bool _hasSelectedProcess;
        private bool _hasSelectedProcessAccessLimit;
        private bool _hasSelectedProcessNetworkConnections;
        private bool _hasSelectedProcessWithoutNetworkConnections;
        private IReadOnlyList<ProcessNetworkConnection> _selectedProcessNetworkConnections = [];

        public ProcessInspectorViewModel(IEventBus eventBus)
            : base(eventBus)
        {
        }

        public string DetailsText { get => _detailsText; private set => SetField(ref _detailsText, value, nameof(DetailsText)); }
        public string HandlesText { get => _handlesText; private set => SetField(ref _handlesText, value, nameof(HandlesText)); }
        public string NetworkText { get => _networkText; private set => SetField(ref _networkText, value, nameof(NetworkText)); }
        public string ServicesText { get => _servicesText; private set => SetField(ref _servicesText, value, nameof(ServicesText)); }
        public string StartupText { get => _startupText; private set => SetField(ref _startupText, value, nameof(StartupText)); }
        public string NoProcessSelectedText { get => _noProcessSelectedText; private set => SetField(ref _noProcessSelectedText, value, nameof(NoProcessSelectedText)); }
        public string AccessLimitedText { get => _accessLimitedText; private set => SetField(ref _accessLimitedText, value, nameof(AccessLimitedText)); }
        public string AccessLimitedDescriptionText { get => _accessLimitedDescriptionText; private set => SetField(ref _accessLimitedDescriptionText, value, nameof(AccessLimitedDescriptionText)); }
        public string PidText { get => _pidText; private set => SetField(ref _pidText, value, nameof(PidText)); }
        public string PublisherText { get => _publisherText; private set => SetField(ref _publisherText, value, nameof(PublisherText)); }
        public string StartTimeText { get => _startTimeText; private set => SetField(ref _startTimeText, value, nameof(StartTimeText)); }
        public string CpuText { get => _cpuText; private set => SetField(ref _cpuText, value, nameof(CpuText)); }
        public string MemoryText { get => _memoryText; private set => SetField(ref _memoryText, value, nameof(MemoryText)); }
        public string PathText { get => _pathText; private set => SetField(ref _pathText, value, nameof(PathText)); }
        public string CommandLineText { get => _commandLineText; private set => SetField(ref _commandLineText, value, nameof(CommandLineText)); }
        public string HandlesSearchPlaceholderText { get => _handlesSearchPlaceholderText; private set => SetField(ref _handlesSearchPlaceholderText, value, nameof(HandlesSearchPlaceholderText)); }
        public string HandlesUnavailableText { get => _handlesUnavailableText; private set => SetField(ref _handlesUnavailableText, value, nameof(HandlesUnavailableText)); }
        public string ServicesUnavailableText { get => _servicesUnavailableText; private set => SetField(ref _servicesUnavailableText, value, nameof(ServicesUnavailableText)); }
        public string StartupUnavailableText { get => _startupUnavailableText; private set => SetField(ref _startupUnavailableText, value, nameof(StartupUnavailableText)); }
        public string ProcessNetworkConnectionsText { get => _processNetworkConnectionsText; private set => SetField(ref _processNetworkConnectionsText, value, nameof(ProcessNetworkConnectionsText)); }
        public string SelectedProcessNetworkSummaryText { get => _selectedProcessNetworkSummaryText; private set => SetField(ref _selectedProcessNetworkSummaryText, value, nameof(SelectedProcessNetworkSummaryText)); }
        public string SelectedProcessConnectionTotalText { get => _selectedProcessConnectionTotalText; private set => SetField(ref _selectedProcessConnectionTotalText, value, nameof(SelectedProcessConnectionTotalText)); }
        public string SelectedProcessTcpConnectionCountText { get => _selectedProcessTcpConnectionCountText; private set => SetField(ref _selectedProcessTcpConnectionCountText, value, nameof(SelectedProcessTcpConnectionCountText)); }
        public string SelectedProcessUdpConnectionCountText { get => _selectedProcessUdpConnectionCountText; private set => SetField(ref _selectedProcessUdpConnectionCountText, value, nameof(SelectedProcessUdpConnectionCountText)); }
        public string NetworkSelectProcessText { get => _networkSelectProcessText; private set => SetField(ref _networkSelectProcessText, value, nameof(NetworkSelectProcessText)); }
        public string NetworkNoConnectionsText { get => _networkNoConnectionsText; private set => SetField(ref _networkNoConnectionsText, value, nameof(NetworkNoConnectionsText)); }
        public string ProtocolText { get => _protocolText; private set => SetField(ref _protocolText, value, nameof(ProtocolText)); }
        public string LocalEndpointText { get => _localEndpointText; private set => SetField(ref _localEndpointText, value, nameof(LocalEndpointText)); }
        public string RemoteEndpointText { get => _remoteEndpointText; private set => SetField(ref _remoteEndpointText, value, nameof(RemoteEndpointText)); }
        public string StateText { get => _stateText; private set => SetField(ref _stateText, value, nameof(StateText)); }
        public string LastSeenText { get => _lastSeenText; private set => SetField(ref _lastSeenText, value, nameof(LastSeenText)); }
        public string CopyLocalEndpointText { get => _copyLocalEndpointText; private set => SetField(ref _copyLocalEndpointText, value, nameof(CopyLocalEndpointText)); }
        public string CopyRemoteEndpointText { get => _copyRemoteEndpointText; private set => SetField(ref _copyRemoteEndpointText, value, nameof(CopyRemoteEndpointText)); }
        public string CopyConnectionInfoText { get => _copyConnectionInfoText; private set => SetField(ref _copyConnectionInfoText, value, nameof(CopyConnectionInfoText)); }
        public ProcessRowViewModel? SelectedProcess { get => _selectedProcess; private set => SetField(ref _selectedProcess, value, nameof(SelectedProcess)); }
        public bool HasSelectedProcess { get => _hasSelectedProcess; private set => SetField(ref _hasSelectedProcess, value, nameof(HasSelectedProcess)); }
        public bool HasSelectedProcessAccessLimit { get => _hasSelectedProcessAccessLimit; private set => SetField(ref _hasSelectedProcessAccessLimit, value, nameof(HasSelectedProcessAccessLimit)); }
        public bool HasSelectedProcessNetworkConnections { get => _hasSelectedProcessNetworkConnections; private set => SetField(ref _hasSelectedProcessNetworkConnections, value, nameof(HasSelectedProcessNetworkConnections)); }
        public bool HasSelectedProcessWithoutNetworkConnections { get => _hasSelectedProcessWithoutNetworkConnections; private set => SetField(ref _hasSelectedProcessWithoutNetworkConnections, value, nameof(HasSelectedProcessWithoutNetworkConnections)); }
        public IReadOnlyList<ProcessNetworkConnection> SelectedProcessNetworkConnections { get => _selectedProcessNetworkConnections; private set => SetField(ref _selectedProcessNetworkConnections, value, nameof(SelectedProcessNetworkConnections)); }

        [EventHandler]
        private void ApplyState(ProcessInspectorStateChangedCommand command)
        {
            var state = command.State;
            DetailsText = state.DetailsText;
            HandlesText = state.HandlesText;
            NetworkText = state.NetworkText;
            ServicesText = state.ServicesText;
            StartupText = state.StartupText;
            NoProcessSelectedText = state.NoProcessSelectedText;
            AccessLimitedText = state.AccessLimitedText;
            AccessLimitedDescriptionText = state.AccessLimitedDescriptionText;
            PidText = state.PidText;
            PublisherText = state.PublisherText;
            StartTimeText = state.StartTimeText;
            CpuText = state.CpuText;
            MemoryText = state.MemoryText;
            PathText = state.PathText;
            CommandLineText = state.CommandLineText;
            HandlesSearchPlaceholderText = state.HandlesSearchPlaceholderText;
            HandlesUnavailableText = state.HandlesUnavailableText;
            ServicesUnavailableText = state.ServicesUnavailableText;
            StartupUnavailableText = state.StartupUnavailableText;
            ProcessNetworkConnectionsText = state.ProcessNetworkConnectionsText;
            SelectedProcessNetworkSummaryText = state.SelectedProcessNetworkSummaryText;
            SelectedProcessConnectionTotalText = state.SelectedProcessConnectionTotalText;
            SelectedProcessTcpConnectionCountText = state.SelectedProcessTcpConnectionCountText;
            SelectedProcessUdpConnectionCountText = state.SelectedProcessUdpConnectionCountText;
            NetworkSelectProcessText = state.NetworkSelectProcessText;
            NetworkNoConnectionsText = state.NetworkNoConnectionsText;
            ProtocolText = state.ProtocolText;
            LocalEndpointText = state.LocalEndpointText;
            RemoteEndpointText = state.RemoteEndpointText;
            StateText = state.StateText;
            LastSeenText = state.LastSeenText;
            CopyLocalEndpointText = state.CopyLocalEndpointText;
            CopyRemoteEndpointText = state.CopyRemoteEndpointText;
            CopyConnectionInfoText = state.CopyConnectionInfoText;
            SelectedProcess = state.SelectedProcess;
            HasSelectedProcess = state.HasSelectedProcess;
            HasSelectedProcessAccessLimit = state.HasSelectedProcessAccessLimit;
            HasSelectedProcessNetworkConnections = state.HasSelectedProcessNetworkConnections;
            HasSelectedProcessWithoutNetworkConnections = state.HasSelectedProcessWithoutNetworkConnections;
            SelectedProcessNetworkConnections = state.SelectedProcessNetworkConnections;
        }
    }
}
