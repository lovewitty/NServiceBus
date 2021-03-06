namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Pipeline;
    using Routing;
    using Timeout.Core;
    using Transports;

    class DispatchTimeoutBehavior : PipelineTerminator<ISatelliteProcessingContext>
    {
        public DispatchTimeoutBehavior(IDispatchMessages dispatcher, IPersistTimeouts persister, TransportTransactionMode transportTransactionMode)
        {
            this.dispatcher = dispatcher;
            this.persister = persister;
            dispatchConsistency = GetDispatchConsistency(transportTransactionMode);
        }

        protected override async Task Terminate(ISatelliteProcessingContext context)
        {
            var message = context.Message;
            var timeoutId = message.Headers["Timeout.Id"];

            var timeoutData = await persister.Peek(timeoutId, context.Extensions).ConfigureAwait(false);

            if (timeoutData == null)
            {
                return;
            }

            timeoutData.Headers[Headers.TimeSent] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);
            timeoutData.Headers["NServiceBus.RelatedToTimeoutId"] = timeoutData.Id;

            var outgoingMessage = new OutgoingMessage(message.MessageId, timeoutData.Headers, timeoutData.State);
            var transportOperation = new TransportOperation(outgoingMessage, new UnicastAddressTag(timeoutData.Destination), dispatchConsistency);
            await dispatcher.Dispatch(new TransportOperations(transportOperation), context.Extensions).ConfigureAwait(false);

            var timeoutRemoved = await persister.TryRemove(timeoutId, context.Extensions).ConfigureAwait(false);
            if (!timeoutRemoved)
            {
                // timeout was concurrently removed between Peek and TryRemove. Throw an exception to rollback the dispatched message if possible.
                throw new Exception($"timeout '{timeoutId}' was concurrently processed.");
            }
        }

        static DispatchConsistency GetDispatchConsistency(TransportTransactionMode transportTransactionMode)
        {
            // dispatch message isolated from existing transactions when not using DTC to avoid loosing timeout data when the transaction commit fails.
            return transportTransactionMode == TransportTransactionMode.TransactionScope
                ? DispatchConsistency.Default
                : DispatchConsistency.Isolated;
        }

        readonly DispatchConsistency dispatchConsistency;

        IDispatchMessages dispatcher;
        IPersistTimeouts persister;
    }
}