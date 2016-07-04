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

                Logger.InfoFormat("Giving up First Level Retries for message '{0}'.", GetMessageId(errorContext));
            }

            if (delayedRetriesEnabled)
            {
                var slrRetryContext = new SecondLevelRetryContext
                {
                    Exception = errorContext.Exception,
                    Message = errorContext.Message, //TODO: do we need message body here
                    SecondLevelRetryAttempt = currentDelayedRetryAttempts + 1
                };

                TimeSpan retryDelay;
                if (secondLevelRetryPolicy.TryGetDelay(slrRetryContext, out retryDelay))
                {
                    return new DelayedRetry(retryDelay);
                }

                Logger.WarnFormat("Giving up Second Level Retries for message '{0}'.", GetMessageId(errorContext));
            }

            return new MoveToError();
        }

        //TODO: this needs to be moved from here
        public string GetMessageId(ErrorContext errorContext)
        {
            string messageId;

            if (errorContext.Headers.TryGetValue(Headers.MessageId, out messageId) && !string.IsNullOrEmpty(messageId))
            {
                return messageId;
            }

            return errorContext.MessageId;
        }

        bool immediateRetriesEnabled;
        bool delayedRetriesEnabled;
        int maxImmediateRetries;
        SecondLevelRetryPolicy secondLevelRetryPolicy;

        static ILog Logger = LogManager.GetLogger<RecoverabilityPolicy>();
    }
}