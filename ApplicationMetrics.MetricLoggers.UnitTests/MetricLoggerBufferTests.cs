/*
 * Copyright 2023 Alastair Wyse (https://github.com/alastairwyse/ApplicationMetrics/)
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
using StandardAbstraction;
using NUnit.Framework;
using NSubstitute;

namespace ApplicationMetrics.MetricLoggers.UnitTests
{
    /// <summary>
    /// Unit tests for class ApplicationMetrics.MetricLoggers.MetricLoggerBuffer.
    /// </summary>
    public class MetricLoggerBufferTests
    {
        private IBufferProcessingStrategy mockBufferProcessingStrategy;
        private IConsole mockConsole;
        private IDateTime mockDateTime;
        private IStopwatch mockStopWatch;
        private IGuidProvider mockGuidProvider;
        private CapturingMetricLoggerBuffer testMetricLoggerBuffer;

        [SetUp]
        protected void SetUp()
        {
            mockBufferProcessingStrategy = Substitute.For<IBufferProcessingStrategy>();
            mockConsole = Substitute.For<IConsole>();
            mockDateTime = Substitute.For<IDateTime>();
            mockStopWatch = Substitute.For<IStopwatch>();
            mockStopWatch.Frequency.Returns<Int64>(10000000);
            mockGuidProvider = Substitute.For<IGuidProvider>();
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Millisecond, true, mockDateTime, mockStopWatch, mockGuidProvider);
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
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

            Assert.That(e.Message, Does.StartWith($"Received end '{new MessageProcessingTime().Name}' with no corresponding begin interval metric."));
        }

        [Test]
        public void End_InterleavedModeAndEndIntervalMetricWithNoBegin()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-03 11:36:43.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                ConvertMillisecondsToTicks(250)
            );
            var testBeginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.End(testBeginId, new MessageProcessingTime());
            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            });

            Assert.That(e.Message, Does.StartWith($"Received end '{new MessageProcessingTime().Name}' with BeginId '{testBeginId.ToString()}' with no corresponding begin interval metric."));
        }

        [Test]
        public void End_NonInterleavedModeAndCallingInterleavedMethodOverload()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                GenerateUtcDateTime("2022-09-04 20:39:55.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin()
                ConvertMillisecondsToTicks(11),
                ConvertMillisecondsToTicks(22),
                ConvertMillisecondsToTicks(34)
            );
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            Guid beginId = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBuffer.End(beginId, new MessageProcessingTime());
            });

            Assert.That(e.Message, Does.StartWith($"The overload of the End() method with a Guid parameter cannot be called when the metric logger is running in non-interleaved mode."));
        }

        [Test]
        public void End_InterleavedModeAndCallingNonInterleavedMethodOverload()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                GenerateUtcDateTime("2022-09-04 20:39:55.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin()
                ConvertMillisecondsToTicks(11),
                ConvertMillisecondsToTicks(22),
                ConvertMillisecondsToTicks(34)
            );
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId, new MessageProcessingTime());
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBuffer.End(new MessageProcessingTime());
            });

            Assert.That(e.Message, Does.StartWith($"The overload of the End() method without a Guid parameter cannot be called when the metric logger is running in interleaved mode."));
        }

        [Test]
        public void End_InterleavedModeAndEndIntervalMetricTypeDoesntMatchBegin()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                GenerateUtcDateTime("2022-09-05 22:05:38.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin()
                ConvertMillisecondsToTicks(11),
                ConvertMillisecondsToTicks(22),
                ConvertMillisecondsToTicks(34)
            );
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId, new DiskReadTime());
            var e = Assert.Throws<ArgumentException>(delegate
            {
                testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            });

            Assert.That(e.Message, Does.StartWith($"Metric started with BeginId '92d8985d-6394-4d97-97bf-8aaf95c97214' was a 'MessageProcessingTime' metric, but End() method was called with a 'DiskReadTime' metric."));
        }

        [Test]
        public void CancelBegin_NonInterleavedModeAndCallingInterleavedMethodOverload()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                GenerateUtcDateTime("2022-09-04 20:42:56.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin()
                ConvertMillisecondsToTicks(11),
                ConvertMillisecondsToTicks(22),
                ConvertMillisecondsToTicks(34)
            );
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.CancelBegin(new MessageProcessingTime());
            Guid beginId = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBuffer.CancelBegin(beginId, new MessageProcessingTime());
            });

            Assert.That(e.Message, Does.StartWith($"The overload of the CancelBegin() method with a Guid parameter cannot be called when the metric logger is running in non-interleaved mode."));
        }

        [Test]
        public void CancelBegin_InterleavedModeAndCallingNonInterleavedMethodOverload()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                GenerateUtcDateTime("2022-09-04 20:42:57.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin()
                ConvertMillisecondsToTicks(11),
                ConvertMillisecondsToTicks(22),
                ConvertMillisecondsToTicks(34)
            );
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.CancelBegin(beginId, new MessageProcessingTime());
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBuffer.CancelBegin(new MessageProcessingTime());
            });

            Assert.That(e.Message, Does.StartWith($"The overload of the CancelBegin() method without a Guid parameter cannot be called when the metric logger is running in interleaved mode."));
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            testMetricLoggerBuffer.CancelBegin(new MessageProcessingTime());
            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            });

            Assert.That(e.Message, Does.StartWith($"Received cancel '{new MessageProcessingTime().Name}' with no corresponding begin interval metric."));
        }

        [Test]
        public void CancelBegin_InterleavedModeAndCancelIntervalMetricWithNoBegin()
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId, new MessageProcessingTime());
            testMetricLoggerBuffer.CancelBegin(beginId, new MessageProcessingTime());
            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            });

            Assert.That(e.Message, Does.StartWith($"Received cancel '{new MessageProcessingTime().Name}' with BeginId '{beginId}' with no corresponding begin interval metric."));
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

            Assert.That(e.Message, Does.StartWith($"Received cancel '{new MessageProcessingTime().Name}' with no corresponding begin interval metric."));
        }

        [Test]
        public void CancelBegin_InterleavedModeAndCancelIntervalMetricWithNoBeginAndNoQueuedMetrics()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-03 11:50:49.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                ConvertMillisecondsToTicks(250)
            );
            Guid beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.CancelBegin(beginId, new MessageProcessingTime());
            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            });

            Assert.That(e.Message, Does.StartWith($"Received cancel '{new MessageProcessingTime().Name}' with BeginId '{beginId}' with no corresponding begin interval metric."));
        }

        [Test]
        public void CancelBegin_InterleavedModeAndBeginIdParameterDoesntMatchIntervalMetricType()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                GenerateUtcDateTime("2022-09-05 22:05:38.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin()
                ConvertMillisecondsToTicks(11),
                ConvertMillisecondsToTicks(22),
                ConvertMillisecondsToTicks(34)
            );
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.CancelBegin(beginId, new DiskReadTime());
            var e = Assert.Throws<ArgumentException>(delegate
            {
                testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            });

            Assert.That(e.Message, Does.StartWith($"Metric started with BeginId '92d8985d-6394-4d97-97bf-8aaf95c97214' was a 'MessageProcessingTime' metric, but CancelBegin() method was called with a 'DiskReadTime' metric."));
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
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
        public void Begin_InterleavedModeAndBufferProcessingBetweenBeginAndEnd()
        {
            // Tests that interval metrics are processed correctly when the buffers/queues are processed in between calls to Begin() and End().
            // Also tests that buffers/queues can be processed successfully before interleaved/non-interleaved mode is determined (i.e. before End() or CancelBegin() is called).
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId1 = testMetricLoggerBuffer.Begin(new DiskReadTime());
            Guid beginId2 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.End(beginId1, new DiskReadTime());
            testMetricLoggerBuffer.End(beginId2, new MessageProcessingTime());
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
        public void Begin_NanosecondBaseTimeUnitAndBufferProcessingBetweenBeginAndEnd()
        {
            // Tests that interval metrics are processed correctly when the buffers/queues are processed in between calls to Begin() and End().
            // Also tests that buffers/queues can be processed successfully before interleaved/non-interleaved mode is determined (i.e. before End() or CancelBegin() is called).
            testMetricLoggerBuffer.Dispose();
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Nanosecond, true, mockDateTime, mockStopWatch, mockGuidProvider);
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId1 = testMetricLoggerBuffer.Begin(new DiskReadTime());
            Guid beginId2 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.End(beginId1, new DiskReadTime());
            testMetricLoggerBuffer.End(beginId2, new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(2).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(2).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(2, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(DiskReadTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 11:53:51.100"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(ConvertMillisecondsToNanoseconds(130), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 11:53:51.210"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item2);
            Assert.AreEqual(ConvertMillisecondsToNanoseconds(150), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item3);
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
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
        public void CancelBegin_InterleavedModeAndBufferProcessingBetweenBeginAndCancel()
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId1 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.CancelBegin(beginId1, new MessageProcessingTime());
            Guid beginId2 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId2, new MessageProcessingTime());
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
        public void CancelBegin_NanosecondBaseTimeUnitAndBufferProcessingBetweenBeginAndCancel()
        {
            // Tests that interval metrics are processed correctly when the buffers/queues are processed in between calls to Begin() and Cancel().
            testMetricLoggerBuffer.Dispose();
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Nanosecond, true, mockDateTime, mockStopWatch, mockGuidProvider);
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId1 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.CancelBegin(beginId1, new MessageProcessingTime());
            Guid beginId2 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId2, new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(2).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(1, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 17:01:09.230"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(ConvertMillisecondsToNanoseconds(130), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
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
        public void Begin_InterleavedMode()
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId1 = testMetricLoggerBuffer.Begin(new DiskReadTime());
            testMetricLoggerBuffer.End(beginId1, new DiskReadTime());
            Guid beginId2 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId2, new MessageProcessingTime());
            Guid beginId3 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId3, new MessageProcessingTime());
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
        public void Begin_NanosecondBaseTimeUnitInterleavedMode()
        {
            testMetricLoggerBuffer.Dispose();
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Nanosecond, true, mockDateTime, mockStopWatch, mockGuidProvider);
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId1 = testMetricLoggerBuffer.Begin(new DiskReadTime());
            testMetricLoggerBuffer.End(beginId1, new DiskReadTime());
            Guid beginId2 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId2, new MessageProcessingTime());
            Guid beginId3 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId3, new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(3).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(3, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(DiskReadTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 17:11:42.011"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(ConvertMillisecondsToNanoseconds(12), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 17:11:42.036"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item2);
            Assert.AreEqual(ConvertMillisecondsToNanoseconds(4), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item3);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[2].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-03 17:11:42.055"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[2].Item2);
            Assert.AreEqual(ConvertMillisecondsToNanoseconds(16), testMetricLoggerBuffer.CapturedIntervalMetricEvents[2].Item3);
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
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
        public void Begin_InterleavedModeNestedBeginAndEndCalls()
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId1 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            Guid beginId2 = testMetricLoggerBuffer.Begin(new DiskReadTime());
            testMetricLoggerBuffer.End(beginId2, new DiskReadTime());
            testMetricLoggerBuffer.End(beginId1, new MessageProcessingTime());
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
        public void Begin_NanosecondBaseTimeUnitNestedBeginAndEndCalls()
        {
            // Tests correct logging of metrics where an interval metric's begin and end events are wholly nested within the begin and end events of another type of interval metric
            testMetricLoggerBuffer.Dispose();
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Nanosecond, true, mockDateTime, mockStopWatch, mockGuidProvider);

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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId1 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            Guid beginId2 = testMetricLoggerBuffer.Begin(new DiskReadTime());
            testMetricLoggerBuffer.End(beginId2, new DiskReadTime());
            testMetricLoggerBuffer.End(beginId1, new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(2).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(2, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(DiskReadTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-04 11:33:07.023"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(ConvertMillisecondsToNanoseconds(13), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-04 11:33:07.011"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item2);
            Assert.AreEqual(ConvertMillisecondsToNanoseconds(39), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
        }

        [Test]
        public void Begin_InterleavedModeInterleavedBeginAndEndCalls()
        {
            // Tests correct logging of metrics where an interval metric's begin and end events are interleaved within the begin and end events of the same type of interval metric
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-05 21:59:20.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                ConvertMillisecondsToTicks(11),
                ConvertMillisecondsToTicks(23),
                ConvertMillisecondsToTicks(36),
                ConvertMillisecondsToTicks(50),
                ConvertMillisecondsToTicks(65),
                ConvertMillisecondsToTicks(81),
                ConvertMillisecondsToTicks(98),
                ConvertMillisecondsToTicks(116)
            );
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4"),
                Guid.Parse("20fcbe66-e2b6-47b3-a933-209ac4016f29")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId1 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            Guid beginId2 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            Guid beginId3 = testMetricLoggerBuffer.Begin(new DiskReadTime());
            testMetricLoggerBuffer.End(beginId1, new MessageProcessingTime());
            Guid beginId4 = testMetricLoggerBuffer.Begin(new DiskReadTime());
            testMetricLoggerBuffer.End(beginId2, new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId3, new DiskReadTime());
            testMetricLoggerBuffer.End(beginId4, new DiskReadTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(4).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(4, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-05 21:59:20.011"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(39, testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-05 21:59:20.023"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item2);
            Assert.AreEqual(58, testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item3);
            Assert.IsInstanceOf(typeof(DiskReadTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[2].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-05 21:59:20.036"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[2].Item2);
            Assert.AreEqual(62, testMetricLoggerBuffer.CapturedIntervalMetricEvents[2].Item3);
            Assert.IsInstanceOf(typeof(DiskReadTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[3].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-05 21:59:20.065"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[3].Item2);
            Assert.AreEqual(51, testMetricLoggerBuffer.CapturedIntervalMetricEvents[3].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
        }

        [Test]
        public void Begin_NanosecondBaseTimeUnitInterleavedBeginAndEndCalls()
        {
            // Tests correct logging of metrics where an interval metric's begin and end events are interleaved within the begin and end events of the same type of interval metric
            testMetricLoggerBuffer.Dispose();
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Nanosecond, true, mockDateTime, mockStopWatch, mockGuidProvider);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-05 21:59:20.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                ConvertMillisecondsToTicks(11),
                ConvertMillisecondsToTicks(23),
                ConvertMillisecondsToTicks(36),
                ConvertMillisecondsToTicks(50),
                ConvertMillisecondsToTicks(65),
                ConvertMillisecondsToTicks(81),
                ConvertMillisecondsToTicks(98),
                ConvertMillisecondsToTicks(116)
            );
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4"),
                Guid.Parse("20fcbe66-e2b6-47b3-a933-209ac4016f29")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId1 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            Guid beginId2 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            Guid beginId3 = testMetricLoggerBuffer.Begin(new DiskReadTime());
            testMetricLoggerBuffer.End(beginId1, new MessageProcessingTime());
            Guid beginId4 = testMetricLoggerBuffer.Begin(new DiskReadTime());
            testMetricLoggerBuffer.End(beginId2, new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId3, new DiskReadTime());
            testMetricLoggerBuffer.End(beginId4, new DiskReadTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(4).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(4, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-05 21:59:20.011"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(ConvertMillisecondsToNanoseconds(39), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-05 21:59:20.023"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item2);
            Assert.AreEqual(ConvertMillisecondsToNanoseconds(58), testMetricLoggerBuffer.CapturedIntervalMetricEvents[1].Item3);
            Assert.IsInstanceOf(typeof(DiskReadTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[2].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-05 21:59:20.036"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[2].Item2);
            Assert.AreEqual(ConvertMillisecondsToNanoseconds(62), testMetricLoggerBuffer.CapturedIntervalMetricEvents[2].Item3);
            Assert.IsInstanceOf(typeof(DiskReadTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[3].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-05 21:59:20.065"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[3].Item2);
            Assert.AreEqual(ConvertMillisecondsToNanoseconds(51), testMetricLoggerBuffer.CapturedIntervalMetricEvents[3].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
        }

        [Test]
        public void Begin_NonInterleavedModeIntervalMetricCheckingParameterFalse()
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Millisecond, false, mockDateTime, mockStopWatch, mockGuidProvider);

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
        public void Begin_InterleavedModeNanosecondBaseTimeUnitAndLargeStopWatchElapsedTicksPropertyDoesntOverflow()
        {
            testMetricLoggerBuffer.Dispose();
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Nanosecond, true, mockDateTime, mockStopWatch, mockGuidProvider);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2023-06-06 23:08:12.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                0, 
                92233720368547759
            );
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId, new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(1, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2023-06-06 23:08:12.000"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(Int64.MaxValue, testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);


            mockDateTime.ClearReceivedCalls();
            mockStopWatch.ClearReceivedCalls();
            mockGuidProvider.ClearReceivedCalls();
            mockBufferProcessingStrategy.ClearReceivedCalls();
            testMetricLoggerBuffer.Dispose();
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Nanosecond, true, mockDateTime, mockStopWatch, mockGuidProvider);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2023-06-06 23:08:12.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                0,
                Int64.MaxValue
            );
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214")
            );

            testMetricLoggerBuffer.Start();
            beginId = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId, new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(1, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2023-06-06 23:08:12.000"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(Int64.MaxValue, testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
        }

        [Test]
        public void Begin_NonInterleavedModeNanosecondBaseTimeUnitAndLargeStopWatchElapsedTicksPropertyDoesntOverflow()
        {
            testMetricLoggerBuffer.Dispose();
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Nanosecond, true, mockDateTime, mockStopWatch, mockGuidProvider);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2023-06-06 23:08:12.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                0,
                92233720368547759
            );
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214")
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(1, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2023-06-06 23:08:12.000"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(Int64.MaxValue, testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);


            mockDateTime.ClearReceivedCalls();
            mockStopWatch.ClearReceivedCalls();
            mockGuidProvider.ClearReceivedCalls();
            mockBufferProcessingStrategy.ClearReceivedCalls();
            testMetricLoggerBuffer.Dispose();
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Nanosecond, true, mockDateTime, mockStopWatch, mockGuidProvider);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2023-06-06 23:08:12.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                0,
                Int64.MaxValue
            );
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214")
            );

            testMetricLoggerBuffer.Start();
            testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(1, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2023-06-06 23:08:12.000"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(Int64.MaxValue, testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
        }

        [Test]
        public void Begin_StopwatchFrequencyLessThan10000000()
        {
            mockStopWatch.Frequency.Returns<Int64>(5000000);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-03 17:11:42.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // All these values are 1/2 what they would be with the 'normal' Stopwatch.Frequency value of 10,000,000
                55000,
                115000,
                180000, 
                200000,
                275000,
                355000
            );
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Millisecond, true, mockDateTime, mockStopWatch, mockGuidProvider);

            testMetricLoggerBuffer.Start();
            Guid beginId1 = testMetricLoggerBuffer.Begin(new DiskReadTime());
            testMetricLoggerBuffer.End(beginId1, new DiskReadTime());
            Guid beginId2 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId2, new MessageProcessingTime());
            Guid beginId3 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId3, new MessageProcessingTime());
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
        public void CancelBegin_NonInterleavedMode()
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
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
        public void CancelBegin_InterleavedMode()
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId1 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.CancelBegin(beginId1, new MessageProcessingTime());
            Guid beginId2 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId2, new MessageProcessingTime());
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
        public void CancelBegin_NanosecondBaseTimeUnitInterleavedMode()
        {
            testMetricLoggerBuffer.Dispose();
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Nanosecond, true, mockDateTime, mockStopWatch, mockGuidProvider);
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );

            testMetricLoggerBuffer.Start();
            Guid beginId1 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.CancelBegin(beginId1, new MessageProcessingTime());
            Guid beginId2 = testMetricLoggerBuffer.Begin(new MessageProcessingTime());
            testMetricLoggerBuffer.End(beginId2, new MessageProcessingTime());
            testMetricLoggerBuffer.DequeueAndProcessMetricEvents();
            testMetricLoggerBuffer.Stop();

            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBuffered();
            mockBufferProcessingStrategy.Received(1).NotifyIntervalMetricEventBufferCleared();
            Assert.AreEqual(1, testMetricLoggerBuffer.CapturedIntervalMetricEvents.Count);
            Assert.IsInstanceOf(typeof(MessageProcessingTime), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item1);
            Assert.AreEqual(GenerateUtcDateTime("2022-09-04 17:58:37.036"), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item2);
            Assert.AreEqual(ConvertMillisecondsToNanoseconds(14), testMetricLoggerBuffer.CapturedIntervalMetricEvents[0].Item3);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedCountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedAmountMetricEvents.Count);
            Assert.AreEqual(0, testMetricLoggerBuffer.CapturedStatusMetricEvents.Count);
        }

        [Test]
        public void CancelBegin_NonInterleavedModeIntervalMetricCheckingParameterFalse()
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
            mockGuidProvider.NewGuid().Returns
            (
                Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214"),
                Guid.Parse("a6e36fad-dac6-426c-b2b8-454f04497866"),
                Guid.Parse("4c9219a1-ae82-42ca-8e2b-26991a0eabc4")
            );
            testMetricLoggerBuffer = new CapturingMetricLoggerBuffer(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Millisecond, false, mockDateTime, mockStopWatch, mockGuidProvider);

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

        [Test]
        public void GetStopWatchUtcNow_Frequency10000000()
        {
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-04 12:10:08.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>(ConvertMillisecondsToTicks(120));
            using (var testMetricLoggerBuffer = new MetricLoggerBufferWithProtectedMethods(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Millisecond, true, mockDateTime, mockStopWatch, mockGuidProvider))
            {
                testMetricLoggerBuffer.Start();

                System.DateTime result = testMetricLoggerBuffer.GetStopWatchUtcNow();

                testMetricLoggerBuffer.Stop();
                Assert.AreEqual(GenerateUtcDateTime("2022-09-04 12:10:08.120"), result);
            }
        }

        [Test]
        public void GetStopWatchUtcNow_FrequencyLessThan10000000()
        {
            mockStopWatch.Frequency.Returns<Int64>(2500000);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-04 12:10:08.000")
            );
            // Frequency is 1/4 of 'normal' value (i.e. 2,500,000 instead of 10,000,000), so returning 1/4 the 'normal' ticks, i.e. 300,000 should result in 1,200,000 'normal' ticks... i.e. 120ms
            mockStopWatch.ElapsedTicks.Returns<Int64>(300000);
            using (var testMetricLoggerBuffer = new MetricLoggerBufferWithProtectedMethods(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Millisecond, true, mockDateTime, mockStopWatch, mockGuidProvider))
            {
                testMetricLoggerBuffer.Start();

                System.DateTime result = testMetricLoggerBuffer.GetStopWatchUtcNow();

                testMetricLoggerBuffer.Stop();
                Assert.AreEqual(GenerateUtcDateTime("2022-09-04 12:10:08.120"), result);
            }
        }

        [Test]
        public void GetStopWatchUtcNow_FrequencyGreaterThan10000000()
        {
            mockStopWatch.Frequency.Returns<Int64>(20000000);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-04 12:10:08.000")
            );
            // Frequency is 2x 'normal' value (i.e. 20,000,000 instead of 10,000,000), so returning 2x the 'normal' ticks, i.e. 2,400,000 should result in 1,200,000 'normal' ticks... i.e. 120ms
            mockStopWatch.ElapsedTicks.Returns<Int64>(2400000);
            using (var testMetricLoggerBuffer = new MetricLoggerBufferWithProtectedMethods(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Millisecond, true, mockDateTime, mockStopWatch, mockGuidProvider))
            {
                testMetricLoggerBuffer.Start();

                System.DateTime result = testMetricLoggerBuffer.GetStopWatchUtcNow();

                testMetricLoggerBuffer.Stop();
                Assert.AreEqual(GenerateUtcDateTime("2022-09-04 12:10:08.120"), result);
            }
        }

        [Test]
        public void GetStopWatchUtcNow_FrequencyNot10000000AndOverflowsInt64()
        {
            mockStopWatch.Frequency.Returns<Int64>(5000000);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                GenerateUtcDateTime("2022-09-04 12:10:08.000")
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>(Int64.MaxValue / 2);
            using (var testMetricLoggerBuffer = new MetricLoggerBufferWithProtectedMethods(mockBufferProcessingStrategy, IntervalMetricBaseTimeUnit.Millisecond, true, mockDateTime, mockStopWatch, mockGuidProvider))
            {
                testMetricLoggerBuffer.Start();

                System.DateTime result = testMetricLoggerBuffer.GetStopWatchUtcNow();

                testMetricLoggerBuffer.Stop();
                Assert.AreEqual(System.DateTime.MaxValue.ToUniversalTime(), result);
            }
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
            /// <param name="intervalMetricBaseTimeUnit">The base time unit to use to log interval metrics.</param>
            /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  Note that this parameter only has an effect when running in 'non-interleaved' mode.</param>
            /// <remarks>This constructor should not be used.</remarks>
            public CapturingMetricLoggerBuffer(IBufferProcessingStrategy bufferProcessingStrategy, IntervalMetricBaseTimeUnit intervalMetricBaseTimeUnit, bool intervalMetricChecking)
                : base(bufferProcessingStrategy, intervalMetricBaseTimeUnit, intervalMetricChecking)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.UnitTests.MetricLoggerBufferTests+CapturingMetricLoggerBuffer class.
            /// </summary>
            /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
            /// <param name="intervalMetricBaseTimeUnit">The base time unit to use to log interval metrics.</param>
            /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  Note that this parameter only has an effect when running in 'non-interleaved' mode.</param>
            /// <param name="dateTime">A test (mock) DateTime object.</param>
            /// <param name="stopWatch">A test (mock) Stopwatch object.</param>
            /// <param name="guidProvider">A test (mock) <see cref="IGuidProvider"/> object.</param>
            public CapturingMetricLoggerBuffer(IBufferProcessingStrategy bufferProcessingStrategy, IntervalMetricBaseTimeUnit intervalMetricBaseTimeUnit, bool intervalMetricChecking, IDateTime dateTime, IStopwatch stopWatch, IGuidProvider guidProvider)
                : base(bufferProcessingStrategy, intervalMetricBaseTimeUnit, intervalMetricChecking, dateTime, stopWatch, guidProvider)
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

        /// <summary>
        /// Version of the MetricLoggerBuffer class where private and protected methods are exposed as public so that they can be unit tested.
        /// </summary>
        private class MetricLoggerBufferWithProtectedMethods : MetricLoggerBuffer
        {
            public MetricLoggerBufferWithProtectedMethods(IBufferProcessingStrategy bufferProcessingStrategy, IntervalMetricBaseTimeUnit intervalMetricBaseTimeUnit, bool intervalMetricChecking, IDateTime dateTime, IStopwatch stopWatch, IGuidProvider guidProvider)
                : base(bufferProcessingStrategy, intervalMetricBaseTimeUnit, intervalMetricChecking, dateTime, stopWatch, guidProvider)
            {
            }

            protected override void ProcessCountMetricEvents(Queue<CountMetricEventInstance> countMetricEvents)
            {
            }

            protected override void ProcessAmountMetricEvents(Queue<AmountMetricEventInstance> amountMetricEvents)
            {
            }

            protected override void ProcessStatusMetricEvents(Queue<StatusMetricEventInstance> statusMetricEvents)
            {
            }

            protected override void ProcessIntervalMetricEvents(Queue<Tuple<IntervalMetricEventInstance, long>> intervalMetricEventsAndDurations)
            {
            }

            public new System.DateTime GetStopWatchUtcNow()
            {
                return base.GetStopWatchUtcNow();
            }
        }

        #endregion
    }
}
