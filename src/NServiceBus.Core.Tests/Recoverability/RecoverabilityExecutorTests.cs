namespace NServiceBus.Core.Tests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using NServiceBus.Transports;
    using NUnit.Framework;

    [TestFixture]
    public class RecoverabilityExecutorTests
    {
        //TODO: add tests for slr and error notification raising
        [Test]
        public async Task When_failure_is_caused_by_deserialization_exception_no_retries_should_be_performed()
        {
            var dispatcher = new FakeDispatcher();

            var recoverabilityExecutor = new RecoverabilityExecutor(
                new RecoverabilityPolicy(true, true, 2, new DefaultSecondLevelRetryPolicy(2, TimeSpan.FromSeconds(10))), 
                new DelayedRetryExecutor("input-queue", dispatcher),
                new MoveToErrorsExecutor(dispatcher, "error-queue", new Dictionary<string, string>()),
                noTransactions: false);

            var errorContext = new ErrorContext(new MessageDeserializationException(""), new Dictionary<string, string>(),  "message-id", new MemoryStream(), new TransportTransaction(), 0);

            var retryImmediately = await recoverabilityExecutor.Invoke(errorContext, new FakeEventAggregator());

            Assert.IsFalse(retryImmediately, "Deserialization exception should cause immediate sent to error queue");
            Assert.AreEqual(1, dispatcher.TransportOperations.UnicastTransportOperations.Count());
            Assert.AreEqual("error-queue", dispatcher.TransportOperations.UnicastTransportOperations.First().Destination);
        }

        [Test]
        public async Task When_running_with_no_transactions_no_retries_should_be_performed()
        {
            var dispatcher = new FakeDispatcher();

            var recoverabilityExecutor = new RecoverabilityExecutor(
                new RecoverabilityPolicy(true, true, 2, new DefaultSecondLevelRetryPolicy(2, TimeSpan.FromSeconds(10))),
                new DelayedRetryExecutor("input-queue", dispatcher),
                new MoveToErrorsExecutor(dispatcher, "error-queue", new Dictionary<string, string>()),
                noTransactions: true);

            var errorContext = new ErrorContext(new MessageDeserializationException(""), new Dictionary<string, string>(), "message-id", new MemoryStream(), new TransportTransaction(), 0);

            var retryImmediately = await recoverabilityExecutor.Invoke(errorContext, new FakeEventAggregator());

            Assert.IsFalse(retryImmediately, "Deserialization exception should cause immediate sent to error queue");
            Assert.AreEqual(1, dispatcher.TransportOperations.UnicastTransportOperations.Count());
            Assert.AreEqual("error-queue", dispatcher.TransportOperations.UnicastTransportOperations.First().Destination);
        }

        [Test]
        public async Task When_failure_is_handeled_with_immediate_retries_notification_should_be_raised()
        {
            var dispatcher = new FakeDispatcher();

            var recoverabilityExecutor = new RecoverabilityExecutor(
                new RecoverabilityPolicy(true, true, 2, new DefaultSecondLevelRetryPolicy(2, TimeSpan.FromSeconds(10))),
                new DelayedRetryExecutor("input-queue", dispatcher),
                new MoveToErrorsExecutor(dispatcher, "error-queue", new Dictionary<string, string>()),
                false);

            var errorContext = new ErrorContext(new MessageDeserializationException(""), new Dictionary<string, string>(), "message-id", new MemoryStream(), new TransportTransaction(), 0);

            var eventAggregator = new FakeEventAggregator();

            await recoverabilityExecutor.Invoke(errorContext, new FakeEventAggregator());

            var failure = eventAggregator.GetNotification<MessageToBeRetried>();

            Assert.AreEqual(0, failure.Attempt);
            Assert.AreEqual("test", failure.Exception.Message);
            Assert.AreEqual("someid", failure.Message.MessageId);
        }

        class FakeDispatcher : IDispatchMessages
        {
            public TransportOperations TransportOperations { get; private set; }

            public Task Dispatch(TransportOperations outgoingMessages, ContextBag context)
            {
                TransportOperations = outgoingMessages;
                return TaskEx.CompletedTask;
            }
        }
    }
}