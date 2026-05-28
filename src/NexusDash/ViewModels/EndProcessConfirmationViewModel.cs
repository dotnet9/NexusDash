using CodeWF.EventBus;
using Prism.Commands;
using System.Collections.Generic;

namespace NexusDash.ViewModels
{
    public sealed class EndProcessConfirmationViewModel : EventBusViewModel
    {
        private bool _isEndProcessConfirmationVisible;
        private string _endProcessConfirmationTitleText = "";
        private string _endProcessConfirmationMessageText = "";
        private IReadOnlyList<ProcessTerminationCandidateViewModel> _pendingTerminationCandidates = [];
        private bool _hasSelectedPendingTerminationProcesses;
        private string _cancelText = "";
        private string _confirmText = "";

        public EndProcessConfirmationViewModel(IEventBus eventBus)
            : base(eventBus)
        {
            CancelPendingProcessTerminationCommand = new DelegateCommand(
                () => EventBus.Publish(new CancelPendingProcessTerminationCommand()));
            ConfirmPendingProcessTerminationCommand = new DelegateCommand(
                () => EventBus.Publish(new ConfirmPendingProcessTerminationCommand()));
        }

        public DelegateCommand CancelPendingProcessTerminationCommand { get; }
        public DelegateCommand ConfirmPendingProcessTerminationCommand { get; }
        public bool IsEndProcessConfirmationVisible { get => _isEndProcessConfirmationVisible; private set => SetField(ref _isEndProcessConfirmationVisible, value, nameof(IsEndProcessConfirmationVisible)); }
        public string EndProcessConfirmationTitleText { get => _endProcessConfirmationTitleText; private set => SetField(ref _endProcessConfirmationTitleText, value, nameof(EndProcessConfirmationTitleText)); }
        public string EndProcessConfirmationMessageText { get => _endProcessConfirmationMessageText; private set => SetField(ref _endProcessConfirmationMessageText, value, nameof(EndProcessConfirmationMessageText)); }
        public IReadOnlyList<ProcessTerminationCandidateViewModel> PendingTerminationCandidates { get => _pendingTerminationCandidates; private set => SetField(ref _pendingTerminationCandidates, value, nameof(PendingTerminationCandidates)); }
        public bool HasSelectedPendingTerminationProcesses { get => _hasSelectedPendingTerminationProcesses; private set => SetField(ref _hasSelectedPendingTerminationProcesses, value, nameof(HasSelectedPendingTerminationProcesses)); }
        public string CancelText { get => _cancelText; private set => SetField(ref _cancelText, value, nameof(CancelText)); }
        public string ConfirmText { get => _confirmText; private set => SetField(ref _confirmText, value, nameof(ConfirmText)); }

        [EventHandler]
        private void ApplyState(EndProcessConfirmationStateChangedCommand command)
        {
            var state = command.State;
            IsEndProcessConfirmationVisible = state.IsEndProcessConfirmationVisible;
            EndProcessConfirmationTitleText = state.EndProcessConfirmationTitleText;
            EndProcessConfirmationMessageText = state.EndProcessConfirmationMessageText;
            PendingTerminationCandidates = state.PendingTerminationCandidates;
            HasSelectedPendingTerminationProcesses = state.HasSelectedPendingTerminationProcesses;
            CancelText = state.CancelText;
            ConfirmText = state.ConfirmText;
        }
    }
}
