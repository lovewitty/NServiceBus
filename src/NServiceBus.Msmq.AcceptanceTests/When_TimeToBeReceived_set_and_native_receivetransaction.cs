﻿namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NUnit.Framework;

    public class When_TimeToBeReceived_set_and_native_receivetransaction : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_throw_on_send()
        {
            var exception = Assert.Throws<AggregateException>(async () =>
                await Scenario.Define<Context>()
                    .WithEndpoint<Endpoint>(b => b.When(async (session, c) => await session.SendLocal(new MyMessage())))
                    .Done(c => c.Exceptions.Any())
                    .Run())
                .ExpectFailedMessages();

            Assert.AreEqual(1, exception.FailedMessages.Count);
            StringAssert.EndsWith(
                "Sending messages with a custom TimeToBeReceived is not supported on transactional MSMQ.",
                exception.FailedMessages.Single().Exception.Message);
        }

        public class Context : ScenarioContext
        {
        }
        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>((config, context) =>
                {
                    config.UseTransport<MsmqTransport>().Transactions(TransportTransactionMode.SendsAtomicWithReceive);
                });
            }
            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }

                public async Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    await context.SendLocal(new MyTimeToBeReceivedMessage());
                }
            }
        }

        public class MyMessage : IMessage
        {
        }

        [TimeToBeReceived("00:01:00")]
        public class MyTimeToBeReceivedMessage : IMessage
        {
        }
    }
}
