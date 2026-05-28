using CodeWF.EventBus;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NexusDash.ViewModels
{
    public sealed class ToolTreeViewModel : EventBusViewModel
    {
        private ToolMenuNode? _selectedToolNode;
        private bool _isApplyingState;

        public ToolTreeViewModel(IEventBus eventBus)
            : base(eventBus)
        {
        }

        public ObservableCollection<ToolMenuNode> ToolMenuItems { get; } = new();

        public ToolMenuNode? SelectedToolNode
        {
            get => _selectedToolNode;
            set
            {
                if (!SetField(ref _selectedToolNode, value, nameof(SelectedToolNode)) ||
                    _isApplyingState ||
                    value is not { } selectedNode)
                {
                    return;
                }

                EventBus.Publish(new ToolSelectionRequestedCommand(selectedNode.ToolKey));
            }
        }

        [EventHandler]
        private void ApplyState(ToolTreeStateChangedCommand command)
        {
            _isApplyingState = true;
            try
            {
                SyncCollection(ToolMenuItems, command.State.ToolMenuItems);
                SelectedToolNode = command.State.SelectedToolNode;
            }
            finally
            {
                _isApplyingState = false;
            }
        }

        private static void SyncCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
        {
            var index = 0;
            while (index < source.Count)
            {
                if (index >= target.Count)
                {
                    target.Add(source[index]);
                }
                else if (!EqualityComparer<T>.Default.Equals(target[index], source[index]))
                {
                    target[index] = source[index];
                }

                index++;
            }

            while (target.Count > source.Count)
            {
                target.RemoveAt(target.Count - 1);
            }
        }
    }
}
