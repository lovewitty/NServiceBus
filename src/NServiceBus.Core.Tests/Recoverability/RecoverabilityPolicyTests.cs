namespace NServiceBus.Core.Tests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using NServiceBus.Transports;
    using NUnit.Framework;

    [TestFixture]
    public class RecoverabilityPolicyTests
    {
        [Test]
        public void When_max_immediate_reties_have_not__been_reached_should_return_immediate_retry_action()
        {
            var policy = new RecoverabilityPolicy(true, true, 2, new DefaultSecondLevelRetryPolicy(2, TimeSpan.FromSeconds(2)));
            var errorContext = new ErrorContext(new Exception(), new Dictionary<string, string>(), "message-id", new MemoryStream(), new TransportTransaction(), 2);
            var recoverabilityAction = policy.Invoke(errorContext, 0);

            Assert.IsInstanceOf<ImmediateRetry>(recoverabilityAction, "We should have one immediate retry left. It is second delivery attempt and we configured immediate reties to 2.");
        }

        [Test]
        public void When_max_immediate_retries_exceeded_should_return_action_other_than_immediate_retry()
        {
            var policy = new RecoverabilityPolicy(true, true, 2, new DefaultSecondLevelRetryPolicy(2, TimeSpan.FromSeconds(2)));
            var errorContext = new ErrorContext(new Exception(), new Dictionary<string, string>(), "message-id", new MemoryStream(), new TransportTransaction(), 4);

            var recoverabilityAction = policy.Invoke(errorContext, 0);

            Assert.IsNotInstanceOf<ImmediateRetry>(recoverabilityAction, "When max number of immediate retries exceeded should return action other than ImmediateRetry.");
        }
    }
}