﻿using NUnit.Framework;
using log4net.Appender;
using log4net.Core;

namespace NServiceBus.Logging.Tests.Log4Net
{
    using Loggers.Log4NetAdapter;

    [TestFixture]
    public class LoggerFactoryTests : BaseLoggerFactoryTests
    {

        [Test]
        public void Test()
        {
            var loggerFactory = new LoggerFactory();

            global::log4net.LogManager.ResetConfiguration();
            Configurator.Basic(new ConsoleAppender { Threshold = Level.All });

            var log = loggerFactory.GetLogger("Test");

            Assert.IsInstanceOf<Log>(log);

            CallAllMethodsOnLog(log);

            log = loggerFactory.GetLogger(typeof(LoggerFactoryTests));

            CallAllMethodsOnLog(log);
        }
    }
}
