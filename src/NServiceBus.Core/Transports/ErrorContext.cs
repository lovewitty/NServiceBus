namespace NServiceBus.Transports
{
    using System;
    using Extensibility;

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
        /// Failed message
        /// </summary>
        public IncomingMessage Message { get; set; }

        /// <summary>
        /// Transport seam message dispatch context.
        /// </summary>
        public ContextBag DispatchContext { get; set; }

        /// <summary>
        /// Number of immediate processing attempts
        /// </summary>
        public int ImmediateProcessingAttempts { get; set; }
    }
}