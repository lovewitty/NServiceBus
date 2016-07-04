﻿namespace NServiceBus
{
    using System;
    using System.Runtime.ExceptionServices;
    using System.Threading.Tasks;
    using Logging;
    using Pipeline;
    using Transports;

    class MoveFaultsToErrorQueueHandler
    {
        public MoveFaultsToErrorQueueHandler(CriticalError criticalError, FailureInfoStorage failureInfoStorage, MoveToErrorsExecutor moveToErrorsExecutor)
        {
            this.criticalError = criticalError;
            this.failureInfoStorage = failureInfoStorage;
            this.moveToErrorsExecutor = moveToErrorsExecutor;
        }

        public void MarkForFutureHandling(ITransportReceiveContext context, Exception ex)
        {
            failureInfoStorage.MarkForMovingToErrorQueue(context.Message.MessageId, ExceptionDispatchInfo.Capture(ex));

            context.AbortReceiveOperation();
        }

        public async Task<bool> HandleIfPreviouslyFailed(ITransportReceiveContext context)
        {
            var message = context.Message;

            var failureInfo = failureInfoStorage.GetFailureInfoForMessage(message.MessageId);

            if (failureInfo.MoveToErrorQueue)
            {
                await MoveMessageToErrorQueue(context, failureInfo.Exception).ConfigureAwait(false);

                return true;
            }
            return false;
        }

        public async Task MoveMessageToErrorQueue(ITransportReceiveContext context, Exception exception)
        {
            var message = context.Message;

            try
            {
                Logger.Error($"Moving message '{message.MessageId}' to the error queue because processing failed due to an exception:", exception);

                failureInfoStorage.ClearFailureInfoForMessage(message.MessageId);

                await moveToErrorsExecutor.MoveToErrorQueue(message, exception, context.Extensions.Get<TransportTransaction>()).ConfigureAwait(false);

                await context.RaiseNotification(new MessageFaulted(message, exception)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                criticalError.Raise("Failed to forward message to error queue", ex);

                throw;
            }
        }

        CriticalError criticalError;
        FailureInfoStorage failureInfoStorage;
        MoveToErrorsExecutor moveToErrorsExecutor;
        static ILog Logger = LogManager.GetLogger<MoveFaultsToErrorQueueHandler>();
    }
}