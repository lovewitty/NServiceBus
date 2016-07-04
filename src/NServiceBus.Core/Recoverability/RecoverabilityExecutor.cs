namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Logging;
    using Transports;

    class RecoverabilityExecutor
    {
        public RecoverabilityExecutor(IRecoverabilityPolicy recoverabilityPolicy, DelayedRetryExecutor delayedRetryExecutor, MoveToErrorsExecutor moveToErrorsExecutor, bool noTransactions)
        {
            this.recoverabilityPolicy = recoverabilityPolicy;
            this.delayedRetryExecutor = delayedRetryExecutor;
            this.moveToErrorsExecutor = moveToErrorsExecutor;
            this.noTransactions = noTransactions;
        }

        //TODO: register eventAggregator in DI
        public async Task<bool> Invoke(ErrorContext errorContext, IEventAggregator eventAggregator)
        {
            if (noTransactions)
            {
                await MoveToError(eventAggregator, errorContext).ConfigureAwait(false);

                return false;
            }

            var retryImmediately = await PerformRecoverabilityAction(errorContext, eventAggregator).ConfigureAwait(false);

            return retryImmediately;
        }

        async Task<bool> PerformRecoverabilityAction(ErrorContext errorContext, IEventAggregator eventAggregator)
        {
            var currentSlrAttempts = DelayedRetryExecutor.GetNumberOfRetries(errorContext.Headers);

            var recoveryAction = recoverabilityPolicy.Invoke(errorContext, currentSlrAttempts);

            if (recoveryAction is ImmediateRetry)
            {
                await RaiseImmediateRetryNotifications(eventAggregator, errorContext).ConfigureAwait(false);

                return true;
            }

            if (recoveryAction is DelayedRetry)
            {
                await DeferMessage(recoveryAction as DelayedRetry, eventAggregator, errorContext, currentSlrAttempts).ConfigureAwait(false);

                return false;
            }

            if (recoveryAction is MoveToError)
            {
                await MoveToError(eventAggregator, errorContext).ConfigureAwait(false);

                return false;
            }

            throw new Exception("Unknown recoverability action returned from RecoverabilityPolicy");
        }

        Task RaiseImmediateRetryNotifications(IEventAggregator eventAggregator, ErrorContext errorContext)
        {
            var message = errorContext.Message;

            Logger.Info($"First Level Retry is going to retry message '{message.MessageId}' because of an exception:", errorContext.Exception);

            return eventAggregator.Raise(new MessageToBeRetried(errorContext.NumberOfDeliveryAttempts - 1, TimeSpan.Zero, message, errorContext.Exception));
        }

        async Task MoveToError(IEventAggregator eventAggregator, ErrorContext errorContext)
        {
            var message = errorContext.Message;

            Logger.Error($"Moving message '{message.MessageId}' to the error queue because processing failed due to an exception:", errorContext.Exception);

            await moveToErrorsExecutor.MoveToErrorQueue(message, errorContext.Exception, errorContext.TransportTransaction).ConfigureAwait(false);

            await eventAggregator.Raise(new MessageFaulted(message, errorContext.Exception)).ConfigureAwait(false);
        }

        async Task DeferMessage(DelayedRetry action, IEventAggregator eventAggregator, ErrorContext errorContext, int currentSlrAttempts)
        {
            var message = errorContext.Message;

            Logger.Warn($"Second Level Retry will reschedule message '{message.MessageId}' after a delay of {action.Delay} because of an exception:", errorContext.Exception);

            await delayedRetryExecutor.Retry(message, action.Delay, currentSlrAttempts, errorContext.TransportTransaction).ConfigureAwait(false);

            await eventAggregator.Raise(new MessageToBeRetried(currentSlrAttempts + 1, action.Delay, message, errorContext.Exception)).ConfigureAwait(false);
        }

        IRecoverabilityPolicy recoverabilityPolicy;
        DelayedRetryExecutor delayedRetryExecutor;
        MoveToErrorsExecutor moveToErrorsExecutor;
        bool noTransactions;

        static ILog Logger = LogManager.GetLogger<SecondLevelRetriesHandler>();
    }
}