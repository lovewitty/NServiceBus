namespace NServiceBus.TransportTests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Transports;

    public class When_on_message_throws : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_call_on_error(TransportTransactionMode transactionMode)
        {
            var onErrorCalled = new TaskCompletionSource<ErrorContext>();

            OnTestTimeout(() => onErrorCalled.SetCanceled());

            await StartPump(context =>
            {
                throw new Exception("Simulated exception");
            },
                context =>
                {
                    onErrorCalled.SetResult(context);

                    return Task.FromResult(false);
                }, transactionMode);

            await SendMessage(InputQueueName);

            var errorContext = await onErrorCalled.Task;

            Assert.AreEqual(errorContext.Exception.Message, "Simulated exception");
            Assert.AreEqual(1, errorContext.NumberOfDeliveryAttempts);
        }
    }
}