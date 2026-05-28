using CodeWF.EventBus;

namespace NexusDash.ViewModels
{
    public sealed class ProcessExplorerViewModel : EventBusViewModel
    {
        public ProcessExplorerViewModel(
            IEventBus eventBus,
            ProcessListViewModel processList,
            ProcessInspectorViewModel inspector)
            : base(eventBus)
        {
            ProcessList = processList;
            Inspector = inspector;
        }

        public ProcessListViewModel ProcessList { get; }
        public ProcessInspectorViewModel Inspector { get; }
    }
}
