using ReactiveUI;
using System;
using System.Globalization;

namespace NexusDash.ViewModels
{
    public sealed class ProcessTerminationCandidateViewModel : ReactiveObject
    {
        private readonly Action<ProcessTerminationCandidateViewModel> _selectionChanged;
        private readonly string _unavailableText;
        private bool _isSelected = true;

        public ProcessTerminationCandidateViewModel(
            ProcessRowViewModel process,
            string relationText,
            string unavailableText,
            int displayOrder,
            int terminationOrder,
            Action<ProcessTerminationCandidateViewModel> selectionChanged)
        {
            Process = process;
            RelationText = relationText;
            _unavailableText = unavailableText;
            DisplayOrder = displayOrder;
            TerminationOrder = terminationOrder;
            _selectionChanged = selectionChanged;
        }

        public ProcessRowViewModel Process { get; }
        public int Pid => Process.Pid;
        public string PidText => string.Format(CultureInfo.CurrentCulture, "PID {0}", Pid);
        public string Name => Process.Name;
        public string RelationText { get; }
        public int DisplayOrder { get; }
        public int TerminationOrder { get; }
        public string ExecutablePathText => string.IsNullOrWhiteSpace(Process.ExecutablePath)
            ? _unavailableText
            : Process.ExecutablePath!;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                this.RaiseAndSetIfChanged(ref _isSelected, value);
                _selectionChanged(this);
            }
        }
    }
}
