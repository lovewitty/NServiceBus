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
        public RecoverabilityPolicy(int maxImmediateRetries, SecondLevelRetryPolicy secondLevelRetryPolicy)
        {
            this.maxImmediateRetries = maxImmediateRetries;
            this.secondLevelRetryPolicy = secondLevelRetryPolicy;
        }

        //TODO: discuss metadata support for policy
        public RecoverabilityAction Invoke(ErrorContext errorContext, int currentSlrAttempts)
        {
            if (errorContext.ImmediateProcessingAttempts <= maxImmediateRetries)
            {
                return new ImmediateRetry();
            }

            Logger.InfoFormat("Giving up First Level Retries for message '{0}'.", errorContext.Message.MessageId);


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

            return new MoveToError();
        }

        static int GetNumberOfRetries(Dictionary<string, string> headers)
        {
            string value;
            if (headers.TryGetValue(Headers.Retries, out value))
            {
                int i;
                if (int.TryParse(value, out i))
                {
                    return i;
                }
            }
            return 0;
        }

        int maxImmediateRetries;
        SecondLevelRetryPolicy secondLevelRetryPolicy;

        static ILog Logger = LogManager.GetLogger<RecoverabilityPolicy>();
    }
}