using CodeWF.EventBus;

namespace NexusDash.ViewModels
{
    public sealed class OperationLogPaneViewModel : EventBusViewModel
    {
        private string _operationLogText = "";

        public OperationLogPaneViewModel(IEventBus eventBus)
            : base(eventBus)
        {
        }

        public string OperationLogText
        {
            get => _operationLogText;
            private set => SetField(ref _operationLogText, value, nameof(OperationLogText));
        }

        [EventHandler]
        private void ApplyState(OperationLogStateChangedCommand command)
        {
            OperationLogText = command.State.OperationLogText;
        }
    }
}
