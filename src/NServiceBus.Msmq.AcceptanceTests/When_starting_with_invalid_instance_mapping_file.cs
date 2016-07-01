﻿namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NUnit.Framework;

    public class When_starting_with_invalid_instance_mapping_file
    {
        [Test]
        public async Task Should_throw_at_startup()
        {
            var exception = Assert.ThrowsAsync<AggregateException>(() => Scenario.Define<ScenarioContext>()
                .WithEndpoint<SenderWithInvalidMappingFile>()
                .Done(c => c.EndpointsStarted)
                .Run());

            //TODO improve exception assertion
            Assert.That(exception.InnerException, Is.Not.Null);
        }

        public class SenderWithInvalidMappingFile : EndpointConfigurationBuilder
        {
            public SenderWithInvalidMappingFile()
            {
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "instance-mapping.xml");

                // e.g. spelling error in endpoint:
                File.WriteAllText(filePath,
@"<endpoints>
    <endpoind name=""someReceiver"">
        <instance discriminator=""1""/>
        <instance discriminator=""2""/>
    </endpoind>
</endpoints>");

                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<MsmqTransport>().Routing()
                        .RouteToEndpoint(typeof(Message), "someReceiver");
                });
            }
        }

        public class Message : ICommand
        {
        }
    }
}