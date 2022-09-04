/*
 * Copyright 2022 Alastair Wyse (https://github.com/alastairwyse/ApplicationMetrics/)
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

using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using NSubstitute;
using StandardAbstraction;


namespace ApplicationMetrics.MetricLoggers.UnitTests
{
    /// <summary>
    /// Unit tests for class ApplicationMetrics.MetricLoggers.MetricLoggerBuffer.
    /// </summary>
    public class MetricLoggerBufferTests
    {
        // Duplicate Begin()s in non-interleaved

        // End with no begin

        // Cancel with no begin
        //   And also where nothing is currently stored... different path in code

        // Buffer process between begin and end

        // BeginCancelBeginBufferProcessingBetweenBeginAndCancelBeginSuccessTests()

        // Different types of metrics are interleaved

        // Begin/End with 'intervalMetricChecking' set to false

        // Begin/Cancel test

        // CancelBeginQueueMaintenanceSuccessTests()
        //   Tests that the rebuilding of the interval metric queue performed by the CancelBegin() method preserves the queue order for the interval metrics that are not cancelled
        //   Note this test was created specifically to test a previous implementation of MetricLoggerBuffer where cancelling of an interval metric was performed by the main thread.  
        //   In the current implementation of MetricLoggerBuffer, cancelling is performed by the buffer processing strategy worker thread, and hence this test is equivalent to test CancelBeginSuccessTests().
        //   However, it will be kept for extra thoroughness of testing.

        // CancelBeginLongQueueSuccessTests()
        //   Tests the case where several successive start and end interval metric events exist in the interval metric queue when CancelBegin() is called
        //   Ensures only the most recent end interval metric is removed from the queue
        //   Note this test was created specifically to test a previous implementation of MetricLoggerBuffer where cancelling of an interval metric was performed by the main thread.  
        //   In the current implementation of MetricLoggerBuffer, cancelling is performed by the buffer processing strategy worker thread, and hence this test is equivalent to test CancelBeginSuccessTests().
        //   However, it will be kept for extra thoroughness of testing.

        // CancelBeginStartIntervalMetricInEventStoreSuccessTests()
        // Tests the case where CancelBegin() is called, and the start interval metric to cancel is stored in the start interval metric event store
        //   Expects that the start interval metric is correctly removed from the start interval metric event store
        //   Note this test was created specifically to test a previous implementation of MetricLoggerBuffer where cancelling of an interval metric was performed by the main thread.  
        //   In the current implementation of MetricLoggerBuffer, cancelling is performed by the buffer processing strategy worker thread, and hence this test is equivalent to test CancelBeginSuccessTests().
        //   However, it will be kept for extra thoroughness of testing.

        // Cancel with no begin 'intervalMetricChecking' set to false


        private IBufferProcessingStrategy mockBufferProcessingStrategy;
        private IConsole mockConsole;
        private IDateTime mockDateTime;
        private IStopwatch mockStopWatch;
        private CapturingMetricLoggerBuffer testMetricLoggerBuffer;

        [SetUp]
        protected void SetUp()
        {
            mockBufferProcessingStrategy = Substitute.For<IBufferProcessingStrategy>();
            mockConsole = Substitute.For<IConsole>();
            mockDateTime = Substitute.For<IDateTime>();
            mockStopWatch = Substitute.For<IStopwatch>();
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, true, mockDateTime, mockStopWatch);
        }

        [TearDown]
        protected void TearDown()
        {
            testMetricLoggerBuffer.Dispose();
        }

        [Test]
        public void Start()
        {
            testMetricLoggerBuffer.Start();

            mockStopWatch.Received(1).Reset();
            mockStopWatch.Received(1).Start();
            var throwaway = mockDateTime.Received(1).UtcNow;
            mockBufferProcessingStrategy.Received(1).Start();
        }

        [Test]
        public void Stop()
        {
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(1).Stop();
            mockStopWatch.Received(1).Stop();
        }

        [Test]
        public void Increment()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                GenerateUtcDateTime("2022-09-03 10:41:52.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Increment()
                ConvertMillisecondsToTicks(250),
                ConvertMillisecondsToTicks(510),
                ConvertMillisecondsToTicks(780)
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Increment(new MessageReceived());
            testMetricLoggerBuffer.Increment(new DiskReadOperation());
            testMetricLoggerBuffer.Increment(new MessageReceived());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(3).NotifyCountMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyCountMetricEventBufferCleared();
            Assert.AreEqual(3, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.IsInstanceOf(typeof(MessageReceived), testMetricLoggerBuffer.CapturedCountMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 10:41:52.250"), testMetricLoggerBuffer.CapturedCountMetricEvents[0].Item2);
            Assert.IsInstanceOf(typeof(DiskReadOperation), testMetricLoggerBuffer.CapturedCountMetricEvents[1].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 10:41:52.510"), testMetricLoggerBuffer.CapturedCountMetricEvents[1].Item2);
            Assert.IsInstanceOf(typeof(MessageReceived), testMetricLoggerBuffer.CapturedCountMetricEvents[2].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 10:41:52.780"), testMetricLoggerBuffer.CapturedCountMetricEvents[2].Item2);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
        }

        [Test]
        public void Add()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                GenerateUtcDateTime("2022-09-03 11:22:51.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add()
                ConvertMillisecondsToTicks(250),
                ConvertMillisecondsToTicks(510),
                ConvertMillisecondsToTicks(780)
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Add(new MessageBytesReceived(), 12345);
            testMetricLoggerBuffer.Add(new DiskBytesRead(), 160307);
            testMetricLoggerBuffer.Add(new MessageBytesReceived(), 12347);
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(3).NotifyAmountMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyAmountMetricEventBufferCleared();
            Assert.AreEqual(3, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.IsInstanceOf(typeof(MessageBytesReceived), testMetricLoggerBuffer.CapturedAmountMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 11:22:51.250"), testMetricLoggerBuffer.CapturedAmountMetricEvents[0].Item2);
            Assert.AreEqual(12345, testMetricLoggerBuffer.CapturedAmountMetricEvents[0].Item3);
            Assert.IsInstanceOf(typeof(DiskBytesRead), testMetricLoggerBuffer.CapturedAmountMetricEvents[1].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 11:22:51.510"), testMetricLoggerBuffer.CapturedAmountMetricEvents[1].Item2);
            Assert.AreEqual(160307, testMetricLoggerBuffer.CapturedAmountMetricEvents[1].Item3);
            Assert.IsInstanceOf(typeof(MessageBytesReceived), testMetricLoggerBuffer.CapturedAmountMetricEvents[2].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 11:22:51.780"), testMetricLoggerBuffer.CapturedAmountMetricEvents[2].Item2);
            Assert.AreEqual(12347, testMetricLoggerBuffer.CapturedAmountMetricEvents[2].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
        }

        [Test]
        public void Set()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                GenerateUtcDateTime("2022-09-03 11:26:19.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Set()
                ConvertMillisecondsToTicks(250),
                ConvertMillisecondsToTicks(510),
                ConvertMillisecondsToTicks(780)
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Set(new AvailableMemory(), 301156000);
            testMetricLoggerBuffer.Set(new FreeWorkerThreads(), 12);
            testMetricLoggerBuffer.Set(new AvailableMemory(), 301155987);
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(3).NotifyStatusMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyStatusMetricEventBufferCleared();
            Assert.AreEqual(3, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
            Assert.IsInstanceOf(typeof(AvailableMemory), testMetricLoggerBuffer.CapturedStatusMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 11:26:19.250"), testMetricLoggerBuffer.CapturedStatusMetricEvents[0].Item2);
            Assert.AreEqual(301156000, testMetricLoggerBuffer.CapturedStatusMetricEvents[0].Item3);
            Assert.IsInstanceOf(typeof(FreeWorkerThreads), testMetricLoggerBuffer.CapturedStatusMetricEvents[1].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 11:26:19.510"), testMetricLoggerBuffer.CapturedStatusMetricEvents[1].Item2);
            Assert.AreEqual(12, testMetricLoggerBuffer.CapturedStatusMetricEvents[1].Item3);
            Assert.IsInstanceOf(typeof(AvailableMemory), testMetricLoggerBuffer.CapturedStatusMetricEvents[2].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 11:26:19.780"), testMetricLoggerBuffer.CapturedStatusMetricEvents[2].Item2);
            Assert.AreEqual(301155987, testMetricLoggerBuffer.CapturedStatusMetricEvents[2].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
        }

        [Test]
        public void Begin_NonInterleavedModeAndDuplicateIntervalMetrics()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                GenerateUtcDateTime("2022-09-03 11:36:43.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin()
                ConvertMillisecondsToTicks(250),
                ConvertMillisecondsToTicks(510)
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            });

            Assert.That(e.Message, Does.StartWith($"Received duplicate begin '{new MessageProcessingTime().Name}' metrics."));
        }

        [Test]
        public void End_NonInterleavedModeAndEndIntervalMetricWithNoBegin()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-03 11:36:43.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                ConvertMillisecondsToTicks(250)
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            });

            Assert.That(e.Message, Does.StartWith($"Received end '{new MessageProcessingTime().Name}' with no corresponding start interval metric."));
        }

        [Test]
        public void CancelBegin_NonInterleavedModeAndCancelIntervalMetricWithNoBegin()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-03 11:48:13.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                ConvertMillisecondsToTicks(250),
                ConvertMillisecondsToTicks(510),
                ConvertMillisecondsToTicks(770)
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            testMetricLoggerBuffer.CancelBegin(new MessageProcessingTime());
            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            });

            Assert.That(e.Message, Does.StartWith($"Received cancel '{new MessageProcessingTime().Name}' with no corresponding start interval metric."));
        }

        [Test]
        public void CancelBegin_NonInterleavedModeAndCancelIntervalMetricWithNoBeginAndNoQueuedMetrics()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-03 11:50:49.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                ConvertMillisecondsToTicks(250)
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.CancelBegin(new MessageProcessingTime());
            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            });

            Assert.That(e.Message, Does.StartWith($"Received cancel '{new MessageProcessingTime().Name}' with no corresponding start interval metric."));
        }

        [Test]
        public void Begin_NonInterleavedModeAndBufferProcessingBetweenBeginAndEnd()
        {
            // Tests that interval metrics are processed correctly when the buffers/queues are processed in between calls to Begin() and End().
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-03 11:53:51.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                ConvertMillisecondsToTicks(100),
                ConvertMillisecondsToTicks(210),
                ConvertMillisecondsToTicks(230),
                ConvertMillisecondsToTicks(360)
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Begin(new DiskReadTime());
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.End(new DiskReadTime());
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(2).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(2).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(2, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(DiskReadTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 11:53:51.100"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(130, testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 11:53:51.210"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item2);
            Assert.AreEqual(150, testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
        }

        [Test]
        public void CancelBegin_NonInterleavedModeAndBufferProcessingBetweenBeginAndCancel()
        {
            // Tests that interval metrics are processed correctly when the buffers/queues are processed in between calls to Begin() and Cancel().
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-03 17:01:09.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                ConvertMillisecondsToTicks(100),
                ConvertMillisecondsToTicks(210),
                ConvertMillisecondsToTicks(230),
                ConvertMillisecondsToTicks(360)
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.CancelBegin(new MessageProcessingTime());
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(2).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(1, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 17:01:09.230"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(130, testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
        }

        [Test]
        public void Begin_NonInterleavedMode()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-03 17:11:42.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                ConvertMillisecondsToTicks(11),
                ConvertMillisecondsToTicks(23),
                ConvertMillisecondsToTicks(36),
                ConvertMillisecondsToTicks(40),
                ConvertMillisecondsToTicks(55),
                ConvertMillisecondsToTicks(71)
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Begin(new DiskReadTime());
            testMetricLoggerBuffer.End(new DiskReadTime());
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(3).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(3, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(DiskReadTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 17:11:42.011"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(12, testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 17:11:42.036"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item2);
            Assert.AreEqual(4, testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item3);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[2].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 17:11:42.055"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[2].Item2);
            Assert.AreEqual(16, testMetricLoggerBuffer.CapturedIntervalMetricEvents[2].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
        }

        [Test]
        public void Begin_NonInterleavedModeNestedBeginAndEndCalls()
        {
            // Tests correct logging of metrics where an interval metric's begin and end events are wholly nested within the begin and end events of another type of interval metric
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-04 11:33:07.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                ConvertMillisecondsToTicks(11),
                ConvertMillisecondsToTicks(23),
                ConvertMillisecondsToTicks(36),
                ConvertMillisecondsToTicks(50)
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.Begin(new DiskReadTime());
            testMetricLoggerBuffer.End(new DiskReadTime());
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(2).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(2, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(DiskReadTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-04 11:33:07.023"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(13, testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-04 11:33:07.011"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item2);
            Assert.AreEqual(39, testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
        }

        [Test]
        public void Begin_NonInterleavedModeIntervalMetricCheckingParameterEnabled()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-04 17:56:12.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                ConvertMillisecondsToTicks(11),
                ConvertMillisecondsToTicks(23),
                ConvertMillisecondsToTicks(36),
                ConvertMillisecondsToTicks(50),
                ConvertMillisecondsToTicks(65),
                ConvertMillisecondsToTicks(81)
            );
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, false, mockDateTime, mockStopWatch);

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(3).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(2, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-04 17:56:12.023"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(13, testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-04 17:56:12.065"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item2);
            Assert.AreEqual(16, testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
        }

        [Test]
        public void CancelBegin()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-04 17:58:37.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                ConvertMillisecondsToTicks(11),
                ConvertMillisecondsToTicks(23),
                ConvertMillisecondsToTicks(36),
                ConvertMillisecondsToTicks(50)
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.CancelBegin(new MessageProcessingTime());
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(1, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-04 17:58:37.036"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(14, testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
        }

        [Test]
        public void CancelBegin_NonInterleavedModeIntervalMetricCheckingParameterEnabled()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-04 12:10:08.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                ConvertMillisecondsToTicks(11),
                ConvertMillisecondsToTicks(23),
                ConvertMillisecondsToTicks(36)
            );
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, false, mockDateTime, mockStopWatch);

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            testMetricLoggerBuffer.CancelBegin(new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(1, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-04 12:10:08.011"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(12, testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
        }

        #region Private/Protected Methods

        /// <summary>
        /// Generates as UTC <see cref="System.DateTime"/> from the specified string containing a date in ISO format.
        /// </summary>
        /// <param name="isoFormattedDateString">The date string.</param>
        /// <returns>the DateTime.</returns>
        private System.DateTime GenerateUtcDateTime(String isoFormattedDateString)
        {
            var returnDateTime = System.DateTime.ParseExact(isoFormattedDateString, "yyyy-MM-dd HH:mm:ss.fff", DateTimeFormatInfo.InvariantInfo);
            return System.DateTime.SpecifyKind(returnDateTime, DateTimeKind.Utc);
        }

        /// <summary>
        /// Converts the specified number of ticks to milliseconds.
        /// </summary>
        /// <param name="ticks">The ticks to convert.</param>
        /// <returns>The equivalent number of milliseconds.</returns>
        private Int64 ConvertMillisecondsToTicks(Int32 ticks)
        {
            return (Int64)ticks * 10000;
        }

        #endregion

        #region Inner Classes

        /// <summary>
        /// Implementation of MetricLoggerBuffer which captures and exposes any logged metrics.
        /// </summary>
        /// <remarks>Since <see cref="MetricLoggerBuffer"/> is abstract, this class is used to implemented all tests.</remarks>
        private class CapturingMetricLoggerBuffer : MetricLoggerBuffer
        {
            /// <summary>Holds any count metric event instances passed to the ProcessCountMetricEvents() method.</summary>
            protected List<Tuple<CountMetric, System.DateTime>> capturedCountMetricEvents;
            /// <summary>Holds any amount metric event instances passed to the ProcessAmountMetricEvents() method.</summary>
            protected List<Tuple<AmountMetric, System.DateTime, Int64>> capturedAmountMetricEvents;
            /// <summary>Holds any status metric event instances passed to the ProcessStatusMetricEvents() method.</summary>
            protected List<Tuple<StatusMetric, System.DateTime, Int64>> capturedStatusMetricEvents;
            /// <summary>Holds any interval metric event instances passed to the ProcessIntervalMetricEvents() method.</summary>
            protected List<Tuple<IntervalMetric, System.DateTime, Int64>> capturedIntervalMetricEvents;

            /// <summary>
            /// Holds any count metric event instances passed to the ProcessCountMetricEvents() method.
            /// </summary>
            public List<Tuple<CountMetric, System.DateTime>> CapturedCountMetricEvents
            {
                get { return capturedCountMetricEvents; }
            }

            /// <summary>
            /// Holds any count amount event instances passed to the ProcessAmountMetricEvents() method.
            /// </summary>
            public List<Tuple<AmountMetric, System.DateTime, Int64>> CapturedAmountMetricEvents
            {
                get { return capturedAmountMetricEvents; }
            }

            /// <summary>
            /// Holds any status metric event instances passed to the ProcessStatusMetricEvents() method.
            /// </summary>
            public List<Tuple<StatusMetric, System.DateTime, Int64>> CapturedStatusMetricEvents
            {
                get { return capturedStatusMetricEvents; }
            }

            /// <summary>
            /// Holds any interval metric event instances passed to the ProcessIntervalMetricEvents() method.
            /// </summary>
            public List<Tuple<IntervalMetric, System.DateTime, Int64>> CapturedIntervalMetricEvents
            {
                get { return capturedIntervalMetricEvents; }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.UnitTests.MetricLoggerBufferTests+CapturingMetricLoggerBuffer class.
            /// </summary>
            /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
            /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).</param>
            /// <remarks>This constructor should not be used.</remarks>
            public CapturingMetricLoggerBuffer(IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking)
                : base(bufferProcessingStrategy, intervalMetricChecking)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.UnitTests.MetricLoggerBufferTests+CapturingMetricLoggerBuffer class.
            /// </summary>
            /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
            /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).</param>
            /// <param name="dateTime">A test (mock) DateTime object.</param>
            /// <param name="stopWatch">A test (mock) Stopwatch object.</param>
            public CapturingMetricLoggerBuffer(IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking, IDateTime dateTime, IStopwatch stopWatch)
                : base(bufferProcessingStrategy, intervalMetricChecking, dateTime, stopWatch)
            {
                capturedCountMetricEvents = new List<Tuple<CountMetric, System.DateTime>>();
                capturedAmountMetricEvents = new List<Tuple<AmountMetric, System.DateTime, Int64>>();
                capturedStatusMetricEvents = new List<Tuple<StatusMetric, System.DateTime, Int64>>();
                capturedIntervalMetricEvents = new List<Tuple<IntervalMetric, System.DateTime, Int64>>();
            }

            /// <summary>
            /// Dequeues and processes metric events stored in the internal buffer.
            /// </summary>
            public new void DequeueAndProcessMetricEvents()
            {
                base.DequeueAndProcessMetricEvents();
            }

            protected override void ProcessCountMetricEvents(Queue<CountMetricEventInstance> countMetricEvents)
            {
                foreach (CountMetricEventInstance currentInstance in countMetricEvents)
                {
                    capturedCountMetricEvents.Add(new Tuple<CountMetric, System.DateTime>(currentInstance.Metric, currentInstance.EventTime));
                }
            }

            protected override void ProcessAmountMetricEvents(Queue<AmountMetricEventInstance> amountMetricEvents)
            {
                foreach (AmountMetricEventInstance currentInstance in amountMetricEvents)
                {
                    capturedAmountMetricEvents.Add(new Tuple<AmountMetric, System.DateTime, Int64>(currentInstance.Metric, currentInstance.EventTime, currentInstance.Amount));
                }
            }

            protected override void ProcessStatusMetricEvents(Queue<StatusMetricEventInstance> statusMetricEvents)
            {
                foreach (StatusMetricEventInstance currentInstance in statusMetricEvents)
                {
                    capturedStatusMetricEvents.Add(new Tuple<StatusMetric, System.DateTime, Int64>(currentInstance.Metric, currentInstance.EventTime, currentInstance.Value));
                }
            }
            protected override void ProcessIntervalMetricEvents(Queue<Tuple<IntervalMetricEventInstance, long>> intervalMetricEventsAndDurations)
            {
                foreach (Tuple<IntervalMetricEventInstance, Int64> currentInstance in intervalMetricEventsAndDurations)
                {
                    capturedIntervalMetricEvents.Add(new Tuple<IntervalMetric, System.DateTime, Int64>(currentInstance.Item1.Metric, currentInstance.Item1.EventTime, currentInstance.Item2));
                }
            }
        }

        #endregion
    }
}
