using CodeWF.EventBus;

namespace NexusDash.ViewModels
{
    public sealed class ProcessManagerViewModel : EventBusViewModel
    {
        private string _processOverviewText = "";
        private string _processTreeText = "";
        private string _detailsText = "";

        public ProcessManagerViewModel(IEventBus eventBus, ProcessListViewModel processList)
            : base(eventBus)
        {
            Overview = new ProcessOverviewViewModel(eventBus);
            Explorer = new ProcessExplorerViewModel(eventBus, processList);
            Inspector = new ProcessInspectorViewModel(eventBus);
        }

        public ProcessOverviewViewModel Overview { get; }
        public ProcessExplorerViewModel Explorer { get; }
        public ProcessInspectorViewModel Inspector { get; }

        public string ProcessOverviewText
        {
            get => _processOverviewText;
            private set => SetField(ref _processOverviewText, value, nameof(ProcessOverviewText));
        }

        public string ProcessTreeText
        {
            get => _processTreeText;
            private set => SetField(ref _processTreeText, value, nameof(ProcessTreeText));
        }

        public string DetailsText
        {
            get => _detailsText;
            private set => SetField(ref _detailsText, value, nameof(DetailsText));
        }

        public override void Dispose()
        {
            Overview.Dispose();
            Explorer.Dispose();
            Inspector.Dispose();
            base.Dispose();
        }

        [EventHandler]
        private void ApplyState(ProcessManagerStateChangedCommand command)
        {
            ProcessOverviewText = command.State.ProcessOverviewText;
            ProcessTreeText = command.State.ProcessTreeText;
            DetailsText = command.State.DetailsText;
        }
    }
}
