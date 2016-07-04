namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Extensibility;
    using Routing;
    using Transports;

    class DelayedRetryExecutor
    {
        public DelayedRetryExecutor(string endpointInputQueue, IDispatchMessages dispatcher, string timeoutManagerAddress = null)
        {
            this.timeoutManagerAddress = timeoutManagerAddress;
            this.dispatcher = dispatcher;
            this.endpointInputQueue = endpointInputQueue;
        }

        public Task Retry(IncomingMessage message, TimeSpan delay, int currentRetryAttempt, TransportTransaction transportTransaction)
        {
            //TODO: this needs to be removed
            message.RevertToOriginalBodyIfNeeded();

            var outgoingMessage = new OutgoingMessage(message.MessageId, new Dictionary<string, string>(message.Headers), message.Body);

            outgoingMessage.Headers[Headers.Retries] = (currentRetryAttempt + 1).ToString();
            outgoingMessage.Headers[Headers.RetriesTimestamp] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);

            UnicastAddressTag messageDestination;
            DeliveryConstraint[] deliveryConstraints = null;
            if (timeoutManagerAddress == null)
            {
                // transport supports native deferred messages, directly send to input queue with delay constraint:
                deliveryConstraints = new DeliveryConstraint[]
                {
                    new DelayDeliveryWith(delay)
                };
                messageDestination = new UnicastAddressTag(endpointInputQueue);
            }
            else
            {
                // transport doesn't support native deferred messages, reroute to timeout manager:
                outgoingMessage.Headers[TimeoutManagerHeaders.RouteExpiredTimeoutTo] = endpointInputQueue;
                outgoingMessage.Headers[TimeoutManagerHeaders.Expire] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow + delay);
                messageDestination = new UnicastAddressTag(timeoutManagerAddress);
            }

            var transportOperations = new TransportOperations(new TransportOperation(outgoingMessage, messageDestination, deliveryConstraints: deliveryConstraints));

            var dispatchContext = new ContextBag();
            dispatchContext.Set(transportTransaction);

            return dispatcher.Dispatch(transportOperations, dispatchContext);
        }

        public static int GetNumberOfRetries(Dictionary<string, string> headers)
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

        readonly IDispatchMessages dispatcher;
        readonly string endpointInputQueue;
        string timeoutManagerAddress;
    }
}