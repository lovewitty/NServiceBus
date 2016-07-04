namespace NServiceBus
{
    using Transports;

    interface IRecoverabilityPolicy
    {
        RecoverabilityAction Invoke(ErrorContext errorContext, int currentDelayedRetryAttempts);
    }
}