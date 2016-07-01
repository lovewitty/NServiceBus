namespace NServiceBus
{
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
        //TODO: we probably want to reverse the bool result that is returned
        public async Task<bool> Invoke(ErrorContext errorContext, IEventAggregator eventAggregator)
        {
            //When running with no transactions we do best effort move to errors
            if (noTransactions)
            {
                await MoveToError(eventAggregator, errorContext).ConfigureAwait(false);

                return true;
            }

            var currentSlrAttempts = DelayedRetryExecutor.GetNumberOfRetries(errorContext.Message.Headers);
            
            //TODO: this should be wrapped in try-catch 
            var recoveryAction = recoverabilityPolicy.Invoke(errorContext, currentSlrAttempts); //????

            if (recoveryAction is ImmediateRetry)
            {
                return false;
            }

            if (recoveryAction is DelayedRetry)
            {
                await DeferMessage(recoveryAction as DelayedRetry, eventAggregator, errorContext, currentSlrAttempts).ConfigureAwait(false);

                return true;
            }

            if (recoveryAction is MoveToError)
            {
                await MoveToError(eventAggregator, errorContext).ConfigureAwait(false);
            }

            //TODO: probably we want to throw here 

            return false;
        }


        async Task MoveToError(IEventAggregator eventAggregator, ErrorContext errorContext)
        {
            var message = errorContext.Message;

            Logger.Error($"Moving message '{message.MessageId}' to the error queue because processing failed due to an exception:", errorContext.Exception);

            await moveToErrorsExecutor.MoveToErrorQueue(message, errorContext.Exception, errorContext.DispatchContext).ConfigureAwait(false);

            await eventAggregator.Raise(new MessageFaulted(message, errorContext.Exception)).ConfigureAwait(false);
        }

        async Task DeferMessage(DelayedRetry action, IEventAggregator eventAggregator, ErrorContext errorContext, int currentSlrAttempts)
        {
            var message = errorContext.Message;

            Logger.Warn($"Second Level Retry will reschedule message '{message.MessageId}' after a delay of {action.Delay} because of an exception:", errorContext.Exception);

            await delayedRetryExecutor.Retry(message, action.Delay, currentSlrAttempts, errorContext.DispatchContext).ConfigureAwait(false);

            await eventAggregator.Raise(new MessageToBeRetried(currentSlrAttempts + 1, action.Delay, message, errorContext.Exception)).ConfigureAwait(false);
        }

        IRecoverabilityPolicy recoverabilityPolicy;
        DelayedRetryExecutor delayedRetryExecutor;
        MoveToErrorsExecutor moveToErrorsExecutor;
        readonly bool noTransactions;

        static ILog Logger = LogManager.GetLogger<SecondLevelRetriesHandler>();
    }
}