namespace NServiceBus.TransportTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;

    public class When_sending_from_on_error : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_dispatch_the_message(TransportTransactionMode transactionMode)
        {
            var messageReceived = new TaskCompletionSource<bool>();

            OnTestTimeout(() => messageReceived.SetResult(false));

            await StartPump(context =>
            {
                if (context.Headers.ContainsKey("FromOnError"))
                {
                    messageReceived.SetResult(true);
                    return Task.FromResult(0);
                }

                throw new Exception("Simulated exception");
            },
                context =>
                {
                    SendMessage(InputQueueName, new Dictionary<string, string> { { "FromOnError", "true" } });

                    return Task.FromResult(false);
                }, transactionMode);

            await SendMessage(InputQueueName);

            Assert.True(await messageReceived.Task, "Message not dispatched properly");
        }
    }
}