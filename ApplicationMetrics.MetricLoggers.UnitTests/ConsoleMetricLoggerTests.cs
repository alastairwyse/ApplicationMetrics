/*
 * Copyright 2015 Alastair Wyse (https://github.com/alastairwyse/ApplicationMetrics/)
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#pragma warning disable 1591

using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using NSubstitute;
using StandardAbstraction;

namespace ApplicationMetrics.MetricLoggers.UnitTests
{
    /// <summary>
    /// Unit tests for class ApplicationMetrics.MetricLoggers.ConsoleMetricLogger.
    /// </summary>
    public class ConsoleMetricLoggerTests
    {
        /* 
         * NOTE: As most of the work of the ConsoleMetricLogger class is done by a worker thread, many of the tests in this class rely on checking the behavior of the worker thread.
         *       This creates an issue in the unit test code as NUnit does not catch exceptions thrown on the worker thread via the Assert.Throws() method.
         *         To work around this an ExceptionStorer object is injected into the test class.  Any exceptions thrown are checked in this object rather than via the Assert.Throws() method (see test method IncrementDatabaseInsertException() as an example).
         */

        private IConsole mockConsole;
        private IDateTime mockDateTime;
        private IStopwatch mockStopWatch;
        private IGuidProvider mockGuidProvider;
        private ManualResetEvent workerThreadLoopIterationCompleteSignal;
        private LoopingWorkerThreadBufferProcessor bufferProcessor;
        private ConsoleMetricLogger testConsoleMetricLogger;
        private const string separatorString = ": ";

        [SetUp]
        protected void SetUp()
        {
            mockConsole = Substitute.For<IConsole>();
            mockDateTime = Substitute.For<IDateTime>();
            mockStopWatch = Substitute.For<IStopwatch>();
            mockStopWatch.Frequency.Returns<Int64>(10000000);
            mockGuidProvider = Substitute.For<IGuidProvider>();
            workerThreadLoopIterationCompleteSignal = new ManualResetEvent(false);
            bufferProcessor = new LoopingWorkerThreadBufferProcessor(10, workerThreadLoopIterationCompleteSignal, 1);
            testConsoleMetricLogger = new ConsoleMetricLogger(bufferProcessor, IntervalMetricBaseTimeUnit.Millisecond, true, mockConsole, mockDateTime, mockStopWatch, mockGuidProvider);
        }

        [TearDown]
        protected void TearDown()
        {
            bufferProcessor.Dispose();
            testConsoleMetricLogger.Dispose();
            workerThreadLoopIterationCompleteSignal.Dispose();
        }

        [Test]
        public void Increment()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 12, 15, 20, 19)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Increment()
                ConvertMillisecondsToTicks(2000),
                ConvertMillisecondsToTicks(4000),
                ConvertMillisecondsToTicks(6000)
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 15, 20, 29));

            testConsoleMetricLogger.Increment(new MessageReceived());
            testConsoleMetricLogger.Increment(new DiskReadOperation());
            testConsoleMetricLogger.Increment(new MessageReceived());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(3).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 15, 20, 29));
            mockConsole.Received(1).WriteLine(new MessageReceived().Name + separatorString + "2");
            mockConsole.Received(1).WriteLine(new DiskReadOperation().Name + separatorString + "1");
        }

        [Test]
        public void Add()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 12, 15, 20, 19)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add()
                ConvertMillisecondsToTicks(2000),
                ConvertMillisecondsToTicks(4000),
                ConvertMillisecondsToTicks(6000)
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 15, 20, 29));

            testConsoleMetricLogger.Add(new MessageBytesReceived(), 1024);
            testConsoleMetricLogger.Add(new DiskBytesRead(), 3049);
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 2048);
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(3).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 15, 20, 29));
            mockConsole.Received(1).WriteLine(new MessageBytesReceived().Name + separatorString + "3072");
            mockConsole.Received(1).WriteLine(new DiskBytesRead().Name + separatorString + "3049");
        }

        [Test]
        public void Set()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 14, 22, 55, 00)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Set()
                ConvertMillisecondsToTicks(1000),
                ConvertMillisecondsToTicks(3000),
                ConvertMillisecondsToTicks(6000)
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 14, 22, 55, 07));

            testConsoleMetricLogger.Set(new AvailableMemory(), 80740352);
            testConsoleMetricLogger.Set(new FreeWorkerThreads(), 8);
            testConsoleMetricLogger.Set(new AvailableMemory(), 714768384);
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(3).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 14, 22, 55, 07));
            mockConsole.Received(1).WriteLine(new AvailableMemory().Name + separatorString + "714768384");
            mockConsole.Received(1).WriteLine(new FreeWorkerThreads().Name + separatorString + "8");
        }

        [Test]
        public void BeginEnd()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 14, 22, 54, 00)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin() /  End()
                ConvertMillisecondsToTicks(1000),
                ConvertMillisecondsToTicks(3250),
                ConvertMillisecondsToTicks(6987),
                ConvertMillisecondsToTicks(7123),
                ConvertMillisecondsToTicks(8124),
                ConvertMillisecondsToTicks(9125)
            );
            mockGuidProvider.NewGuid().Returns(Guid.NewGuid());
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 14, 22, 58, 05));

            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.Begin(new DiskReadTime());
            testConsoleMetricLogger.End(new DiskReadTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(6).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 14, 22, 58, 05));
            mockConsole.Received(1).WriteLine(new DiskReadTime().Name + separatorString + "3737");
            mockConsole.Received(1).WriteLine(new MessageProcessingTime().Name + separatorString + "7124");
        }

        [Test]
        public void BeginEnd_NanosecondBaseTimeUnit()
        {
            testConsoleMetricLogger.Dispose();
            testConsoleMetricLogger = new ConsoleMetricLogger(bufferProcessor, IntervalMetricBaseTimeUnit.Nanosecond, true, mockConsole, mockDateTime, mockStopWatch, mockGuidProvider);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 14, 22, 54, 00)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin() /  End()
                ConvertMillisecondsToTicks(1000),
                ConvertMillisecondsToTicks(3250),
                ConvertMillisecondsToTicks(6987),
                ConvertMillisecondsToTicks(7123),
                ConvertMillisecondsToTicks(8124),
                ConvertMillisecondsToTicks(9125)
            );
            mockGuidProvider.NewGuid().Returns(Guid.NewGuid());
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 14, 22, 58, 05));

            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.Begin(new DiskReadTime());
            testConsoleMetricLogger.End(new DiskReadTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(6).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 14, 22, 58, 05));
            mockConsole.Received(1).WriteLine(new DiskReadTime().Name + separatorString + ConvertMillisecondsToNanoseconds(3737));
            mockConsole.Received(1).WriteLine(new MessageProcessingTime().Name + separatorString + ConvertMillisecondsToNanoseconds(7124));
        }

        [Test]
        public void LogCountOverTimeUnitAggregate()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 12, 15, 39, 10, 000)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Increment()
                ConvertMillisecondsToTicks(250),
                ConvertMillisecondsToTicks(500),
                ConvertMillisecondsToTicks(750),
                ConvertMillisecondsToTicks(1000),
                ConvertMillisecondsToTicks(1250)
            );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for calls to LogCountOverTimeUnitAggregates()
                2000
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 15, 39, 14, 250));

            testConsoleMetricLogger.DefineMetricAggregate(new MessageReceived(), TimeUnit.Second, "MessagesReceivedPerSecond", "The number of messages received per second");
            for (int i = 0; i < 5; i++)
            {
                testConsoleMetricLogger.Increment(new MessageReceived());
            }
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(5).ElapsedTicks;
            var throwAway3 = mockStopWatch.Received(1).ElapsedMilliseconds;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 15, 39, 14, 250));
            mockConsole.Received(1).WriteLine(new MessageReceived().Name + separatorString + "5");
            mockConsole.Received(1).WriteLine("MessagesReceivedPerSecond" + separatorString + "2.5");
        }

        [Test]
        public void LogCountOverTimeUnitAggregate_NoInstances()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 7, 11, 23, 30, 42, 000)
            );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for calls to LogCountOverTimeUnitAggregates()
                5000
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 7, 11, 23, 30, 52, 000));

            testConsoleMetricLogger.DefineMetricAggregate(new MessageReceived(), TimeUnit.Second, "MessagesReceivedPerSecond", "The number of messages received per second");
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(50);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(1).ElapsedMilliseconds;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 7, 11, 23, 30, 52, 000));
            mockConsole.Received(1).WriteLine("MessagesReceivedPerSecond" + separatorString + "0");
        }

        [Test]
        public void LogAmountOverCountAggregate()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 12, 17, 56, 18, 000)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add() / Increment()
                ConvertMillisecondsToTicks(1000),
                ConvertMillisecondsToTicks(2000),
                ConvertMillisecondsToTicks(3000),
                ConvertMillisecondsToTicks(4000),
                ConvertMillisecondsToTicks(5000),
                ConvertMillisecondsToTicks(6000),
                ConvertMillisecondsToTicks(7000),
                ConvertMillisecondsToTicks(8000)
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 17, 56, 27, 000));

            testConsoleMetricLogger.DefineMetricAggregate(new MessageBytesReceived(), new MessageReceived(), "BytesReceivedPerMessage", "The number of bytes received per message");
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 2);
            testConsoleMetricLogger.Increment(new MessageReceived());
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 6);
            testConsoleMetricLogger.Increment(new MessageReceived());
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 3);
            testConsoleMetricLogger.Increment(new MessageReceived());
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 7);
            testConsoleMetricLogger.Increment(new MessageReceived());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(8).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 17, 56, 27, 000));
            mockConsole.Received(1).WriteLine(new MessageReceived().Name + separatorString + "4");
            mockConsole.Received(1).WriteLine(new MessageBytesReceived().Name + separatorString + "18");
            mockConsole.Received(1).WriteLine("BytesReceivedPerMessage" + separatorString + "4.5");
        }

        [Test]
        public void LogAmountOverCountAggregate_NoInstances()
        {
            // Tests defining an amount over count aggregate, where no instances of the underlying count metric have been logged

            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 12, 17, 56, 18, 000)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add()
                ConvertMillisecondsToTicks(1000),
                ConvertMillisecondsToTicks(2000),
                ConvertMillisecondsToTicks(3000),
                ConvertMillisecondsToTicks(4000)
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 17, 56, 28, 500));

            testConsoleMetricLogger.DefineMetricAggregate(new MessageBytesReceived(), new MessageReceived(), "BytesReceivedPerMessage", "The number of bytes received per message");
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 2);
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 6);
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 3);
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 7);
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(50);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 17, 56, 28, 500));
            mockConsole.Received(1).WriteLine(new MessageBytesReceived().Name + separatorString + "18");
        }

        [Test]
        public void LogAmountOverTimeUnitAggregate()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 12, 15, 39, 10, 000)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add()
                ConvertMillisecondsToTicks(250),
                ConvertMillisecondsToTicks(500),
                ConvertMillisecondsToTicks(750),
                ConvertMillisecondsToTicks(1000),
                ConvertMillisecondsToTicks(1250)
            );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for calls to LogCountOverTimeUnitAggregates()
                2000
            );

            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 15, 39, 10, 125));

            testConsoleMetricLogger.DefineMetricAggregate(new MessageBytesReceived(), TimeUnit.Second, "MessageBytesPerSecond", "The number of message bytes received per second");
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 149);
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 257);
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 439);
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 271);
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 229);
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(5).ElapsedTicks;
            var throwAway3 = mockStopWatch.Received(1).ElapsedMilliseconds;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 15, 39, 10, 125));
            mockConsole.Received(1).WriteLine(new MessageBytesReceived().Name + separatorString + "1345");
            mockConsole.Received(1).WriteLine("MessageBytesPerSecond" + separatorString + "672.5");
        }

        [Test]
        public void LogAmountOverTimeUnitAggregate_NoInstances()
        {
            // Tests defining an amount over time unit aggregate, where no instances of the underlying amount metric have been logged

            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 7, 11, 23, 30, 42, 000)
            );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for calls to LogCountOverTimeUnitAggregates()
                5000
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 7, 11, 23, 30, 42, 125));

            testConsoleMetricLogger.DefineMetricAggregate(new MessageBytesReceived(), TimeUnit.Second, "MessageBytesPerSecond", "The number of message bytes received per second");
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(50);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(1).ElapsedMilliseconds;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 7, 11, 23, 30, 42, 125));
            mockConsole.Received(1).WriteLine("MessageBytesPerSecond" + separatorString + "0");
        }

        [Test]
        public void LogAmountOverAmountAggregate()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Add()
                new System.DateTime(2014, 07, 12, 15, 39, 10, 250),
                new System.DateTime(2014, 07, 12, 15, 39, 10, 500),
                new System.DateTime(2014, 07, 12, 15, 39, 10, 750),
                new System.DateTime(2014, 07, 12, 15, 39, 11, 000),
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 12, 15, 39, 10, 000)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add()
                ConvertMillisecondsToTicks(250),
                ConvertMillisecondsToTicks(500),
                ConvertMillisecondsToTicks(750),
                ConvertMillisecondsToTicks(1000)
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 15, 39, 10, 125));

            testConsoleMetricLogger.DefineMetricAggregate(new MessageBytesReceived(), new DiskBytesRead(), "MessageBytesReceivedPerDiskBytesRead", "The number of message bytes received per disk bytes read");
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 149);
            testConsoleMetricLogger.Add(new DiskBytesRead(), 257);
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 439);
            testConsoleMetricLogger.Add(new DiskBytesRead(), 271);
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 15, 39, 10, 125));
            mockConsole.Received(1).WriteLine(new MessageBytesReceived().Name + separatorString + "588");
            mockConsole.Received(1).WriteLine(new DiskBytesRead().Name + separatorString + "528");
            mockConsole.Received(1).WriteLine("MessageBytesReceivedPerDiskBytesRead" + separatorString + "1.1136363636363635");
        }

        [Test]
        public void LogAmountOverAmountAggregate_NoNumeratorInstances()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 12, 15, 39, 10, 000)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add()
                ConvertMillisecondsToTicks(250),
                ConvertMillisecondsToTicks(500)
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 15, 39, 10, 125));

            testConsoleMetricLogger.DefineMetricAggregate(new MessageBytesReceived(), new DiskBytesRead(), "MessageBytesReceivedPerDiskBytesRead", "The number of message bytes received per disk bytes read");
            testConsoleMetricLogger.Add(new DiskBytesRead(), 257);
            testConsoleMetricLogger.Add(new DiskBytesRead(), 271);
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(2).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 15, 39, 10, 125));
            mockConsole.Received(1).WriteLine("MessageBytesReceivedPerDiskBytesRead" + separatorString + "0");
        }

        [Test]
        public void LogAmountOverAmountAggregate_NoDenominatorInstances()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 12, 15, 39, 10, 000)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add()
                ConvertMillisecondsToTicks(250),
                ConvertMillisecondsToTicks(500)
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 15, 39, 10, 125));

            testConsoleMetricLogger.DefineMetricAggregate(new MessageBytesReceived(), new DiskBytesRead(), "MessageBytesReceivedPerDiskBytesRead", "The number of message bytes received per disk bytes read");
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 149);
            testConsoleMetricLogger.Add(new MessageBytesReceived(), 439);
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(50);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(2).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 15, 39, 10, 125));
            mockConsole.Received(1).WriteLine(new MessageBytesReceived().Name + separatorString + "588");
        }

        [Test]
        public void LogIntervalOverCountAggregate()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 16, 23, 01, 16, 999)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin() / End()
                ConvertMillisecondsToTicks(0),
                ConvertMillisecondsToTicks(120),
                ConvertMillisecondsToTicks(120),
                ConvertMillisecondsToTicks(850),
                ConvertMillisecondsToTicks(1975),
                ConvertMillisecondsToTicks(1980)
            );
            mockGuidProvider.NewGuid().Returns(Guid.NewGuid());
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 16, 23, 01, 17, 060));

            testConsoleMetricLogger.DefineMetricAggregate(new MessageProcessingTime(), new MessageReceived(), "ProcessingTimePerMessage", "The average time to process each message");
            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Increment(new MessageReceived());
            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Increment(new MessageReceived());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(6).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 16, 23, 01, 17, 060));
            mockConsole.Received(1).WriteLine(new MessageReceived().Name + separatorString + "2");
            mockConsole.Received(1).WriteLine(new MessageProcessingTime().Name + separatorString + "1245");
            mockConsole.Received(1).WriteLine("ProcessingTimePerMessage" + separatorString + "622.5");
        }

        [Test]
        public void LogIntervalOverCountAggregate_NoInstances()
        {
            // Tests defining an interval over count aggregate, where no instances of the underlying count metric have been logged

            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 16, 23, 01, 16, 999)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin() / End()
                ConvertMillisecondsToTicks(0),
                ConvertMillisecondsToTicks(120),
                ConvertMillisecondsToTicks(2850),
                ConvertMillisecondsToTicks(3975)
            );
            mockGuidProvider.NewGuid().Returns(Guid.NewGuid());
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 16, 23, 01, 17, 060));

            testConsoleMetricLogger.DefineMetricAggregate(new MessageProcessingTime(), new MessageReceived(), "ProcessingTimePerMessage", "The average time to process each message");
            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(50);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 16, 23, 01, 17, 061));
            mockConsole.Received(1).WriteLine(new MessageProcessingTime().Name + separatorString + "1245");
        }

        [Test]
        public void LogIntervalOverTotalRunTimeAggregate()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 19, 17, 33, 50, 000)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin() / End()
                ConvertMillisecondsToTicks(1000),
                ConvertMillisecondsToTicks(1789),
                ConvertMillisecondsToTicks(2058),
                ConvertMillisecondsToTicks(6032)
            );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for calls to LogIntervalOverTotalRunTimeAggregates()
                6300
            );
            mockGuidProvider.NewGuid().Returns(Guid.NewGuid());
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 19, 17, 33, 51, 125));

            testConsoleMetricLogger.DefineMetricAggregate(new MessageProcessingTime(), "MessageProcessingTimePercentage", "The amount of time spent processing messages as a percentage of total run time");
            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            var throwAway3 = mockStopWatch.Received(1).ElapsedMilliseconds;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 19, 17, 33, 51, 125));
            mockConsole.Received(1).WriteLine(new MessageProcessingTime().Name + separatorString + "4763");
            mockConsole.Received(1).WriteLine("MessageProcessingTimePercentage" + separatorString + "0.756031746031746");
        }

        [Test]
        public void LogIntervalOverTotalRunTimeAggregate_NanosecondBaseTimeUnit()
        {
            testConsoleMetricLogger.Dispose();
            testConsoleMetricLogger = new ConsoleMetricLogger(bufferProcessor, IntervalMetricBaseTimeUnit.Nanosecond, true, mockConsole, mockDateTime, mockStopWatch, mockGuidProvider);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 19, 17, 33, 50, 000)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin() / End()
                ConvertMillisecondsToTicks(1000),
                ConvertMillisecondsToTicks(1789),
                ConvertMillisecondsToTicks(2058),
                ConvertMillisecondsToTicks(6032),
                // Returns for calls to LogIntervalOverTotalRunTimeAggregates()
                ConvertMillisecondsToTicks(6300)
            );
            mockGuidProvider.NewGuid().Returns(Guid.NewGuid());
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 19, 17, 33, 51, 125));

            testConsoleMetricLogger.DefineMetricAggregate(new MessageProcessingTime(), "MessageProcessingTimePercentage", "The amount of time spent processing messages as a percentage of total run time");
            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(5).ElapsedTicks;
            var throwAway3 = mockStopWatch.DidNotReceive().ElapsedMilliseconds;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 19, 17, 33, 51, 125));
            mockConsole.Received(1).WriteLine(new MessageProcessingTime().Name + separatorString + ConvertMillisecondsToNanoseconds(4763));
            mockConsole.Received(1).WriteLine("MessageProcessingTimePercentage" + separatorString + "0.756031746031746");
        }

        [Test]
        public void LogIntervalOverTotalRunTimeAggregate_ZeroElapsedTime()
        {
            // Tests that an aggregate is not logged when no time has elapsed

            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 19, 17, 33, 50, 000)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin() / End()
                ConvertMillisecondsToTicks(1000),
                ConvertMillisecondsToTicks(1789),
                ConvertMillisecondsToTicks(2058),
                ConvertMillisecondsToTicks(6032)
            );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for calls to LogIntervalOverTotalRunTimeAggregates()
                0
            );
            mockGuidProvider.NewGuid().Returns(Guid.NewGuid());
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 19, 17, 33, 51, 125));

            testConsoleMetricLogger.DefineMetricAggregate(new MessageProcessingTime(), "MessageProcessingTimePercentage", "The amount of time spent processing messages as a percentage of total run time");
            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            var throwAway3 = mockStopWatch.Received(1).ElapsedMilliseconds;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 19, 17, 33, 51, 125));
            mockConsole.Received(1).WriteLine(new MessageProcessingTime().Name + separatorString + "4763");
            // Can't use DidNotRecieve() with partial matching parameter, hence check that only 5 total calls were received (4 for writing the title, plus the above metric)
            Assert.AreEqual(5, mockConsole.ReceivedCalls().Count());
        }

        [Test]
        public void LogIntervalOverTotalRunTimeAggregate_NanosecondBaseTimeUnitZeroElapsedTime()
        {
            // Tests that an aggregate is not logged when no time has elapsed

            testConsoleMetricLogger.Dispose();
            testConsoleMetricLogger = new ConsoleMetricLogger(bufferProcessor, IntervalMetricBaseTimeUnit.Nanosecond, true, mockConsole, mockDateTime, mockStopWatch, mockGuidProvider);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 07, 19, 17, 33, 50, 000)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin() / End()
                ConvertMillisecondsToTicks(1000),
                ConvertMillisecondsToTicks(1789),
                ConvertMillisecondsToTicks(2058),
                ConvertMillisecondsToTicks(6032),
                // Returns for calls to LogIntervalOverTotalRunTimeAggregates()
                ConvertMillisecondsToTicks(0)
            );
            mockGuidProvider.NewGuid().Returns(Guid.NewGuid());
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 19, 17, 33, 51, 125));

            testConsoleMetricLogger.DefineMetricAggregate(new MessageProcessingTime(), "MessageProcessingTimePercentage", "The amount of time spent processing messages as a percentage of total run time");
            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Begin(new MessageProcessingTime());
            testConsoleMetricLogger.End(new MessageProcessingTime());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(5).ElapsedTicks;
            var throwAway3 = mockStopWatch.DidNotReceive().ElapsedMilliseconds;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 19, 17, 33, 51, 125));
            mockConsole.Received(1).WriteLine(new MessageProcessingTime().Name + separatorString + ConvertMillisecondsToNanoseconds(4763));
            // Can't use DidNotRecieve() with partial matching parameter, hence check that only 5 total calls were received (4 for writing the title, plus the above metric)
            Assert.AreEqual(5, mockConsole.ReceivedCalls().Count());
        }

        #region Private/Protected Methods

        /// <summary>
        /// Sets expected mock method calls for clearing the console and writing the title banner.
        /// </summary>
        /// <param name="startDateTime">The initial DateTime written to the title banner.</param>
        private void SetWriteTitleExpectedReceives(System.DateTime startDateTime)
        {
            mockConsole.Received(1).Clear();
            mockConsole.Received(2).WriteLine("---------------------------------------------------");
            mockConsole.Received(1).WriteLine($"-- Application metrics as of {startDateTime.ToString("yyyy-MM-dd HH:mm:ss")} --");
        }

        /// <summary>
        /// Converts the specified number of ticks to milliseconds.
        /// </summary>
        /// <param name="milliseconds">The milliseconds to convert.</param>
        /// <returns>The equivalent number of ticks.</returns>
        private Int64 ConvertMillisecondsToTicks(Int32 milliseconds)
        {
            return (Int64)milliseconds * 10000;
        }

        /// <summary>
        /// Converts the specified number of ticks to nanoseconds.
        /// </summary>
        /// <param name="milliseconds">The milliseconds to convert.</param>
        /// <returns>The equivalent number of nanoseconds.</returns>
        private Int64 ConvertMillisecondsToNanoseconds(Int32 milliseconds)
        {
            return (Int64)milliseconds * 1000000;
        }

        #endregion
    }
}
