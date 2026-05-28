using AtomUI.Controls.Primitives;
using AtomUI.Desktop.Controls;
using CodeWF.EventBus;
using ReactiveUI;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NexusDash.ViewModels
{
    public sealed class ToolTreeViewModel : EventBusViewModel
    {
        private INavMenuNode? _selectedToolNode;
        private bool _isApplyingState;

        public ToolTreeViewModel(IEventBus eventBus)
            : base(eventBus)
        {
        }

        public ObservableCollection<INavMenuNode> ToolMenuItems { get; } = new();
        public IList<TreeNodePath> ToolMenuDefaultOpenPaths { get; private set; } = [];

        public INavMenuNode? SelectedToolNode
        {
            get => _selectedToolNode;
            set
            {
                if (!SetField(ref _selectedToolNode, value, nameof(SelectedToolNode)) ||
                    _isApplyingState ||
                    value?.ItemKey?.Value is not { } toolKey)
                {
                    return;
                }

                EventBus.Publish(new ToolSelectionRequestedCommand(toolKey));
            }
        }

        [EventHandler]
        private void ApplyState(ToolTreeStateChangedCommand command)
        {
            _isApplyingState = true;
            try
            {
                SyncCollection(ToolMenuItems, command.State.ToolMenuItems);
                ToolMenuDefaultOpenPaths = command.State.ToolMenuDefaultOpenPaths;
                this.RaisePropertyChanged(nameof(ToolMenuDefaultOpenPaths));
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
