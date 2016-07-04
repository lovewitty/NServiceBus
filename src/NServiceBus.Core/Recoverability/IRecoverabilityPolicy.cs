namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using Logging;
    using Transports;

    interface IRecoverabilityPolicy
    {
        RecoverabilityAction Invoke(ErrorContext errorContext, int currentSlrAttempts);
    }

    class RecoverabilityPolicy : IRecoverabilityPolicy
    {
        public RecoverabilityPolicy(bool immediateRetriesEnabled, bool delayedRetriesEnabled, int maxImmediateRetries, SecondLevelRetryPolicy secondLevelRetryPolicy)
        {
            this.immediateRetriesEnabled = immediateRetriesEnabled;
            this.delayedRetriesEnabled = delayedRetriesEnabled;
            this.maxImmediateRetries = maxImmediateRetries;
            this.secondLevelRetryPolicy = secondLevelRetryPolicy;
        }

        //TODO: discuss metadata support for policy
        public RecoverabilityAction Invoke(ErrorContext errorContext, int currentSlrAttempts)
        {
            if (immediateRetriesEnabled)
            {
                if (errorContext.ImmediateProcessingAttempts <= maxImmediateRetries)
                {
                    return new ImmediateRetry();
                }

                Logger.InfoFormat("Giving up First Level Retries for message '{0}'.", errorContext.Message.MessageId);
            }

            if (delayedRetriesEnabled)
            {
                var slrRetryContext = new SecondLevelRetryContext
                {
                    Exception = errorContext.Exception,
                    Message = errorContext.Message, //TODO: do we need message body here
                    SecondLevelRetryAttempt = currentSlrAttempts + 1
                };

                TimeSpan retryDelay;
                if (secondLevelRetryPolicy.TryGetDelay(slrRetryContext, out retryDelay))
                {
                    return new DelayedRetry(retryDelay);
                }

                Logger.WarnFormat("Giving up Second Level Retries for message '{0}'.", errorContext.Message.MessageId);
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