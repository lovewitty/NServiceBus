namespace NServiceBus
{
    using System;
    using Logging;
    using Transports;

    class RecoverabilityPolicy : IRecoverabilityPolicy
    {
        public RecoverabilityPolicy(bool immediateRetriesEnabled, bool delayedRetriesEnabled, int maxImmediateRetries, SecondLevelRetryPolicy secondLevelRetryPolicy)
        {
            this.immediateRetriesEnabled = immediateRetriesEnabled;
            this.delayedRetriesEnabled = delayedRetriesEnabled;
            this.maxImmediateRetries = maxImmediateRetries;
            this.secondLevelRetryPolicy = secondLevelRetryPolicy;
        }

        public RecoverabilityAction Invoke(ErrorContext errorContext, int currentDelayedRetryAttempts)
        {
            if (immediateRetriesEnabled)
            {
                if (errorContext.NumberOfDeliveryAttempts <= maxImmediateRetries)
                {
                    return new ImmediateRetry();
                }

                Logger.InfoFormat("Giving up First Level Retries for message '{0}'.", errorContext.MessageId);
            }

            if (delayedRetriesEnabled)
            {
                var slrRetryContext = new SecondLevelRetryContext
                {
                    Exception = errorContext.Exception,
                    Message = null, //TODO: do we need message body here
                    SecondLevelRetryAttempt = currentDelayedRetryAttempts + 1
                };

                TimeSpan retryDelay;
                if (secondLevelRetryPolicy.TryGetDelay(slrRetryContext, out retryDelay))
                {
                    return new DelayedRetry(retryDelay);
                }

                Logger.WarnFormat("Giving up Second Level Retries for message '{0}'.", errorContext.MessageId);
            }

            return new MoveToError();
        }

        bool immediateRetriesEnabled;
        bool delayedRetriesEnabled;
        int maxImmediateRetries;
        SecondLevelRetryPolicy secondLevelRetryPolicy;

        static ILog Logger = LogManager.GetLogger<RecoverabilityPolicy>();
    }
}