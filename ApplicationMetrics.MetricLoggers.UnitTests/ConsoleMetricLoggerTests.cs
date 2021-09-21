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
            workerThreadLoopIterationCompleteSignal = new ManualResetEvent(false);
            bufferProcessor = new LoopingWorkerThreadBufferProcessor(10, workerThreadLoopIterationCompleteSignal, 1);
            testConsoleMetricLogger = new ConsoleMetricLogger(bufferProcessor, true, mockConsole, mockDateTime, mockStopWatch);
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
                20000000L,
                40000000L,
                60000000L
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 15, 20, 29));

            testConsoleMetricLogger.Increment(new TestMessageReceivedMetric());
            testConsoleMetricLogger.Increment(new TestDiskReadOperationMetric());
            testConsoleMetricLogger.Increment(new TestMessageReceivedMetric());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(3).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 15, 20, 29));
            mockConsole.Received(1).WriteLine(new TestMessageReceivedMetric().Name + separatorString + "2");
            mockConsole.Received(1).WriteLine(new  TestDiskReadOperationMetric().Name + separatorString + "1");
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
                20000000L,
                40000000L,
                60000000L
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 15, 20, 29));

            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(1024));
            testConsoleMetricLogger.Add(new TestDiskBytesReadMetric(3049));
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(2048));
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(3).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 15, 20, 29));
            mockConsole.Received(1).WriteLine(new TestMessageBytesReceivedMetric(0).Name + separatorString + "3072");
            mockConsole.Received(1).WriteLine(new TestDiskBytesReadMetric(0).Name + separatorString + "3049");
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
                10000000L,
                30000000L,
                60000000L
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 14, 22, 55, 07));

            testConsoleMetricLogger.Set(new TestAvailableMemoryMetric(80740352));
            testConsoleMetricLogger.Set(new TestFreeWorkerThreadsMetric(8));
            testConsoleMetricLogger.Set(new TestAvailableMemoryMetric(714768384));
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(3).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 14, 22, 55, 07));
            mockConsole.Received(1).WriteLine(new TestAvailableMemoryMetric(0).Name + separatorString + "714768384");
            mockConsole.Received(1).WriteLine(new TestFreeWorkerThreadsMetric(0).Name + separatorString + "8");
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
                10000000L,
                32500000L,
                69870000L,
                71230000L,
                81240000L,
                91250000L
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 14, 22, 58, 05));

            testConsoleMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.Begin(new TestDiskReadTimeMetric());
            testConsoleMetricLogger.End(new TestDiskReadTimeMetric());
            testConsoleMetricLogger.End(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.End(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(6).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 14, 22, 58, 05));
            mockConsole.Received(1).WriteLine(new TestDiskReadTimeMetric().Name + separatorString + "3737");
            mockConsole.Received(1).WriteLine(new TestMessageProcessingTimeMetric().Name + separatorString + "7124");
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
                2500000L,
                5000000L,
                7500000L,
                10000000L,
                12500000L
            );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for calls to LogCountOverTimeUnitAggregates()
                2000
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 15, 39, 14, 250));

            testConsoleMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "MessagesReceivedPerSecond", "The number of messages received per second");
            for (int i = 0; i < 5; i++)
            {
                testConsoleMetricLogger.Increment(new TestMessageReceivedMetric());
            }
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(5).ElapsedTicks;
            var throwAway3 = mockStopWatch.Received(1).ElapsedMilliseconds;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 15, 39, 14, 250));
            mockConsole.Received(1).WriteLine(new TestMessageReceivedMetric().Name + separatorString + "5");
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

            testConsoleMetricLogger.DefineMetricAggregate(new TestMessageReceivedMetric(), TimeUnit.Second, "MessagesReceivedPerSecond", "The number of messages received per second");
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
                10000000L,
                20000000L,
                30000000L,
                40000000L,
                50000000L,
                60000000L,
                70000000L,
                80000000L
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 17, 56, 27, 000));

            testConsoleMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(0), new TestMessageReceivedMetric(), "BytesReceivedPerMessage", "The number of bytes received per message");
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(2));
            testConsoleMetricLogger.Increment(new TestMessageReceivedMetric());
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(6));
            testConsoleMetricLogger.Increment(new TestMessageReceivedMetric());
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(3));
            testConsoleMetricLogger.Increment(new TestMessageReceivedMetric());
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(7));
            testConsoleMetricLogger.Increment(new TestMessageReceivedMetric());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(8).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 17, 56, 27, 000));
            mockConsole.Received(1).WriteLine(new TestMessageReceivedMetric().Name + separatorString + "4");
            mockConsole.Received(1).WriteLine(new TestMessageBytesReceivedMetric(0).Name + separatorString + "18");
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
                10000000L,
                20000000L,
                30000000L,
                40000000L
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 17, 56, 28, 500));

            testConsoleMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(0), new TestMessageReceivedMetric(), "BytesReceivedPerMessage", "The number of bytes received per message");
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(2));
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(6));
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(3));
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(7));
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(50);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 17, 56, 28, 500));
            mockConsole.Received(1).WriteLine(new TestMessageBytesReceivedMetric(0).Name + separatorString + "18");
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
                2500000L,
                5000000L,
                7500000L,
                10000000L,
                12500000L
            );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for calls to LogCountOverTimeUnitAggregates()
                2000
            );

            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 15, 39, 10, 125));

            testConsoleMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(0), TimeUnit.Second, "MessageBytesPerSecond", "The number of message bytes received per second");
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(149));
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(257));
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(439));
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(271));
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(229));
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(5).ElapsedTicks;
            var throwAway3 = mockStopWatch.Received(1).ElapsedMilliseconds;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 15, 39, 10, 125));
            mockConsole.Received(1).WriteLine(new TestMessageBytesReceivedMetric(0).Name + separatorString + "1345");
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

            testConsoleMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(0), TimeUnit.Second, "MessageBytesPerSecond", "The number of message bytes received per second");
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
                2500000L,
                5000000L,
                7500000L,
                10000000L
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 15, 39, 10, 125));

            testConsoleMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(0), new TestDiskBytesReadMetric(0), "MessageBytesReceivedPerDiskBytesRead", "The number of message bytes received per disk bytes read");
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(149));
            testConsoleMetricLogger.Add(new TestDiskBytesReadMetric(257));
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(439));
            testConsoleMetricLogger.Add(new TestDiskBytesReadMetric(271));
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 15, 39, 10, 125));
            mockConsole.Received(1).WriteLine(new TestMessageBytesReceivedMetric(0).Name + separatorString + "588");
            mockConsole.Received(1).WriteLine(new TestDiskBytesReadMetric(0).Name + separatorString + "528");
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
                2500000L,
                5000000L
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 15, 39, 10, 125));

            testConsoleMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(0), new TestDiskBytesReadMetric(0), "MessageBytesReceivedPerDiskBytesRead", "The number of message bytes received per disk bytes read");
            testConsoleMetricLogger.Add(new TestDiskBytesReadMetric(257));
            testConsoleMetricLogger.Add(new TestDiskBytesReadMetric(271));
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
                2500000L,
                5000000L
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 12, 15, 39, 10, 125));

            testConsoleMetricLogger.DefineMetricAggregate(new TestMessageBytesReceivedMetric(0), new TestDiskBytesReadMetric(0), "MessageBytesReceivedPerDiskBytesRead", "The number of message bytes received per disk bytes read");
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(149));
            testConsoleMetricLogger.Add(new TestMessageBytesReceivedMetric(439));
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(50);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(2).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 12, 15, 39, 10, 125));
            mockConsole.Received(1).WriteLine(new TestMessageBytesReceivedMetric(0).Name + separatorString + "588");
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
                0L,
                1200000L,
                1200000L,
                8500000L,
                19750000L,
                19800000L
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 16, 23, 01, 17, 060));

            testConsoleMetricLogger.DefineMetricAggregate(new TestMessageProcessingTimeMetric(), new TestMessageReceivedMetric(), "ProcessingTimePerMessage", "The average time to process each message");
            testConsoleMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.End(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.Increment(new TestMessageReceivedMetric());
            testConsoleMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.End(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.Increment(new TestMessageReceivedMetric());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(6).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 16, 23, 01, 17, 060));
            mockConsole.Received(1).WriteLine(new TestMessageReceivedMetric().Name + separatorString + "2");
            mockConsole.Received(1).WriteLine(new TestMessageProcessingTimeMetric().Name + separatorString + "1245");
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
                0,
                1200000L,
                28500000L,
                39750000L
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 16, 23, 01, 17, 060));

            testConsoleMetricLogger.DefineMetricAggregate(new TestMessageProcessingTimeMetric(), new TestMessageReceivedMetric(), "ProcessingTimePerMessage", "The average time to process each message");
            testConsoleMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.End(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.End(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();
            // Wait a few more milliseconds so that any unexpected method calls after the signal are caught
            Thread.Sleep(50);

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 16, 23, 01, 17, 061));
            mockConsole.Received(1).WriteLine(new TestMessageProcessingTimeMetric().Name + separatorString + "1245");
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
                10000000L,
                17890000L,
                20580000L,
                60320000L
            );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for calls to LogIntervalOverTotalRunTimeAggregates()
                6300
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 19, 17, 33, 51, 125));

            testConsoleMetricLogger.DefineMetricAggregate(new TestMessageProcessingTimeMetric(), "MessageProcessingTimePercentage", "The amount of time spent processing messages as a percentage of total run time");
            testConsoleMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.End(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.End(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            var throwAway3 = mockStopWatch.Received(1).ElapsedMilliseconds;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 19, 17, 33, 51, 125));
            mockConsole.Received(1).WriteLine(new TestMessageProcessingTimeMetric().Name + separatorString + "4763");
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
                10000000L,
                17890000L,
                20580000L,
                60320000L
            );
            mockStopWatch.ElapsedMilliseconds.Returns<Int64>
            (
                // Returns for calls to LogIntervalOverTotalRunTimeAggregates()
                6300
            );
            // Returns for writing title banner
            mockDateTime.Now.Returns<System.DateTime>(new System.DateTime(2014, 07, 19, 17, 33, 51, 125));

            testConsoleMetricLogger.DefineMetricAggregate(new TestMessageProcessingTimeMetric(), "MessageProcessingTimePercentage", "The amount of time spent processing messages as a percentage of total run time");
            testConsoleMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.End(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.End(new TestMessageProcessingTimeMetric());
            testConsoleMetricLogger.Start();
            workerThreadLoopIterationCompleteSignal.WaitOne();

            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(4).ElapsedTicks;
            var throwAway3 = mockStopWatch.Received(1).ElapsedMilliseconds;
            throwAway1 = mockDateTime.Received(1).Now;
            SetWriteTitleExpectedReceives(new System.DateTime(2014, 07, 19, 17, 33, 51, 125));
            mockConsole.Received(1).WriteLine(new TestMessageProcessingTimeMetric().Name + separatorString + "4763");
            mockConsole.Received(1).WriteLine("MessageProcessingTimePercentage" + separatorString + "0.756031746031746");
        }

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
    }
}
