using CodeWF.EventBus;

namespace NexusDash.ViewModels
{
    public sealed class OperationLogPaneViewModel : EventBusViewModel
    {
        private string _operationLogText = "";
        private string _operationLogContent = "";

        public OperationLogPaneViewModel(IEventBus eventBus)
            : base(eventBus)
        {
        }

        public string OperationLogText
        {
            get => _operationLogText;
            private set => SetField(ref _operationLogText, value, nameof(OperationLogText));
        }

        public string OperationLogContent
        {
            get => _operationLogContent;
            private set => SetField(ref _operationLogContent, value, nameof(OperationLogContent));
        }

        [EventHandler]
        private void ApplyState(OperationLogStateChangedCommand command)
        {
            OperationLogText = command.State.OperationLogText;
            OperationLogContent = command.State.OperationLogContent;
        }
    }
}
