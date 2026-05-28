using CodeWF.EventBus;
using ReactiveUI;
using System;
using System.Collections.Generic;

namespace NexusDash.ViewModels
{
    public abstract class EventBusViewModel : ReactiveObject, IDisposable
    {
        private bool _isDisposed;

        protected EventBusViewModel(IEventBus eventBus)
        {
            EventBus = eventBus;
            EventBus.Subscribe(this);
        }

        protected IEventBus EventBus { get; }

        public virtual void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            EventBus.Unsubscribe(this);
        }

        protected bool SetField<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            this.RaiseAndSetIfChanged(ref field, value, propertyName);
            return true;
        }
    }
}
