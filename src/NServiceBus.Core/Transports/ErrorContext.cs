namespace NServiceBus.Transports
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// The context for messages that has failed processing.
    /// </summary>
    public class ErrorContext
    {
        /// <summary>
        /// Exception that caused the message processing to fail.
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        /// The headers of the failed message.
        /// </summary>
        public Dictionary<string, string> Headers { get; private set; }

        /// <summary>
        /// The native id of the failed message.
        /// </summary>
        public string MessageId { get; private set; }

        /// <summary>
        /// The original body of the failed message.
        /// </summary>
        public Stream BodyStream { get; private set; }

        /// <summary>
        /// Transport transaction for failed receive message.
        /// </summary>
        public TransportTransaction TransportTransaction { get; private set; }

        /// <summary>
        /// Number of immediate processing attempts.
        /// </summary>
        public int NumberOfDeliveryAttempts { get; private set; }

        /// <summary>
        /// Initializes the error context.
        /// </summary>
        public ErrorContext(Exception exception, Dictionary<string, string> headers, string messageId, Stream bodyStream, TransportTransaction transportTransaction, int numberOfDeliveryAttempts)
        {
            Exception = exception;
            Headers = headers;
            MessageId = messageId;
            BodyStream = bodyStream;
            TransportTransaction = transportTransaction;
            NumberOfDeliveryAttempts = numberOfDeliveryAttempts;
        }
    }
}