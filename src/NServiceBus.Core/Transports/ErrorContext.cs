namespace NServiceBus.Transports
{
    using System;

    /// <summary>
    /// The context for messages that has failed processing.
    /// </summary>
    public class ErrorContext
    {
        /// <summary>
        /// Exception that caused the message processing to fail.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Failed incoming message.
        /// </summary>
        public IncomingMessage Message { get; set; }

        /// <summary>
        /// Transport transaction for failed receive message.
        /// </summary>
        public TransportTransaction TransportTransaction { get; set; }

        /// <summary>
        /// Number of immediate processing attempts.
        /// </summary>
        public int NumberOfDeliveryAttempts { get; set; }
    }
}