namespace NServiceBus
{
    using System;

    class DelayedRetry : RecoverabilityAction
    {
        public TimeSpan Delay { get; private set; }

        public DelayedRetry(TimeSpan delay)
        {
            Delay = delay;
        }
    }
}