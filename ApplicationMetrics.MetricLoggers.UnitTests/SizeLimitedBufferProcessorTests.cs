/*
 * Copyright 2021 Alastair Wyse (https://github.com/alastairwyse/ApplicationMetrics/)
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
using System.Threading;
using System.Collections.Generic;
using NUnit.Framework;

namespace ApplicationMetrics.MetricLoggers.UnitTests
{
    /// <summary>
    /// Unit tests for class ApplicationMetrics.MetricLoggers.SizeLimitedBufferProcessorTests.
    /// </summary>
    public class SizeLimitedBufferProcessorTests
    {
        // Some of these tests use Thread.Sleep() statements to synchronise activity between the main thread and buffer processing worker thread, and hence results could be non-deterministic depending on system thread scheduling and performance.
        // Decided to do this, as making things fully deterministic would involve adding more test-only thread synchronising mechanisms (in addition to the existing WorkerThreadBufferProcessorBase.loopIterationCompleteSignal property), which would mean more redundtant statements executing during normal runtime.
        // I think the current implementation strikes a balance between having fully deterministic tests, and not interfering too much with normal runtime operation.

        private ManualResetEvent loopIterationCompleteSignal;
        private SizeLimitedBufferProcessor testSizeLimitedBufferProcessor;
        private CountingMetricLogger testCountingMetricLogger;
        private Int32 millisecondsToWaitBeforeStop;

        [SetUp]
        protected void SetUp()
        {
            loopIterationCompleteSignal = new ManualResetEvent(false);
            testSizeLimitedBufferProcessor = new SizeLimitedBufferProcessor(3, loopIterationCompleteSignal);
            testCountingMetricLogger = new CountingMetricLogger(testSizeLimitedBufferProcessor, IntervalMetricBaseTimeUnit.Millisecond, true);
            millisecondsToWaitBeforeStop = 250;
        }

        [TearDown]
        protected void TearDown()
        {
            testCountingMetricLogger.Dispose();
            testSizeLimitedBufferProcessor.Dispose();
            loopIterationCompleteSignal.Dispose();
        }

        [Test]
        public void Constructor_BufferSizeLimitParameterLessThan1()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                testSizeLimitedBufferProcessor = new SizeLimitedBufferProcessor(0, loopIterationCompleteSignal);
            });

            Assert.That(e.Message, Does.StartWith("Parameter 'bufferSizeLimit' with value 0 cannot be less than 1."));
            Assert.AreEqual(e.ParamName, "bufferSizeLimit");
        }

        [Test]
        public void Stop_NoRemainingEventsAndProcessRemainingEventsSetTrue()
        {
            testCountingMetricLogger.Start();
            testCountingMetricLogger.Add(new MessageBytesReceived(), 100);
            testCountingMetricLogger.Increment(new MessageReceived());
            testCountingMetricLogger.Set(new AvailableMemory(), 1000000);
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(millisecondsToWaitBeforeStop);
            testSizeLimitedBufferProcessor.Stop();
            loopIterationCompleteSignal.WaitOne();

            Assert.AreEqual(1, testCountingMetricLogger.AmountMetricsProcessed);
            Assert.AreEqual(1, testCountingMetricLogger.CountMetricsProcessed);
            Assert.AreEqual(1, testCountingMetricLogger.StatusMetricsProcessed);
            testCountingMetricLogger.Stop();
        }

        [Test]
        public void Stop_NoRemainingEventsAndProcessRemainingEventsSetFalse()
        {
            testCountingMetricLogger.Start();
            testCountingMetricLogger.Add(new MessageBytesReceived(), 100);
            testCountingMetricLogger.Increment(new MessageReceived());
            testCountingMetricLogger.Set(new AvailableMemory(), 1000000);
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(millisecondsToWaitBeforeStop);
            testSizeLimitedBufferProcessor.Stop(false);
            loopIterationCompleteSignal.WaitOne();

            Assert.AreEqual(1, testCountingMetricLogger.AmountMetricsProcessed);
            Assert.AreEqual(1, testCountingMetricLogger.CountMetricsProcessed);
            Assert.AreEqual(1, testCountingMetricLogger.StatusMetricsProcessed);
            testCountingMetricLogger.Stop();
        }

        [Test]
        public void Stop_RemainingEventsAndProcessRemainingEventsSetTrue()
        {
            testCountingMetricLogger.Start();
            testCountingMetricLogger.Add(new MessageBytesReceived(), 100);
            testCountingMetricLogger.Increment(new MessageReceived());
            testCountingMetricLogger.Set(new AvailableMemory(), 1000000);
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(millisecondsToWaitBeforeStop);
            testCountingMetricLogger.Add(new MessageBytesReceived(), 200);
            testSizeLimitedBufferProcessor.Stop();
            loopIterationCompleteSignal.WaitOne();

            Assert.AreEqual(2, testCountingMetricLogger.AmountMetricsProcessed);
            Assert.AreEqual(1, testCountingMetricLogger.CountMetricsProcessed);
            Assert.AreEqual(1, testCountingMetricLogger.StatusMetricsProcessed);
            testCountingMetricLogger.Stop();
        }

        [Test]
        public void Stop_RemainingEventsAndProcessRemainingEventsSetFalse()
        {
            testCountingMetricLogger.Start();
            testCountingMetricLogger.Add(new MessageBytesReceived(), 100);
            testCountingMetricLogger.Increment(new MessageReceived());
            testCountingMetricLogger.Set(new AvailableMemory(), 1000000);
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(millisecondsToWaitBeforeStop);
            testCountingMetricLogger.Add(new MessageBytesReceived(), 200);
            testSizeLimitedBufferProcessor.Stop(false);
            loopIterationCompleteSignal.WaitOne();

            Assert.AreEqual(1, testCountingMetricLogger.AmountMetricsProcessed);
            Assert.AreEqual(1, testCountingMetricLogger.CountMetricsProcessed);
            Assert.AreEqual(1, testCountingMetricLogger.StatusMetricsProcessed);
            testCountingMetricLogger.Stop();
        }

        [Test]
        public void Stop_ExceptionOccursOnWorkerThread()
        {
            testSizeLimitedBufferProcessor = new SizeLimitedBufferProcessor(3, loopIterationCompleteSignal);
            testSizeLimitedBufferProcessor.BufferProcessed += (object sender, EventArgs e) => { throw new Exception("Mock worker thread exception."); };
            testSizeLimitedBufferProcessor.Start();
            testSizeLimitedBufferProcessor.NotifyAmountMetricEventBuffered();
            testSizeLimitedBufferProcessor.NotifyCountMetricEventBuffered();
            testSizeLimitedBufferProcessor.NotifyIntervalMetricEventBuffered();
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events and throw an exception
            Thread.Sleep(millisecondsToWaitBeforeStop);

            Exception e = Assert.Throws<Exception>(delegate
            {
                testSizeLimitedBufferProcessor.Stop();
            });

            Assert.That(e.Message, Does.StartWith("Exception occurred on buffer processing worker thread at "));
            Assert.That(e.InnerException.Message, Does.StartWith("Mock worker thread exception."));
        }

        [Test]
        public void Stop_ExceptionOccursOnWorkerThreadProcessingRemainingEvents()
        {
            testSizeLimitedBufferProcessor = new SizeLimitedBufferProcessor(3, loopIterationCompleteSignal);
            testSizeLimitedBufferProcessor.BufferProcessed += (object sender, EventArgs e) => { throw new Exception("Mock worker thread exception."); };
            testSizeLimitedBufferProcessor.Start();
            testSizeLimitedBufferProcessor.NotifyAmountMetricEventBuffered();
            testSizeLimitedBufferProcessor.NotifyCountMetricEventBuffered();
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(millisecondsToWaitBeforeStop);

            Exception e = Assert.Throws<Exception>(delegate
            {
                testSizeLimitedBufferProcessor.Stop();
            });

            Assert.That(e.Message, Does.StartWith("Exception occurred on buffer processing worker thread at "));
            Assert.That(e.InnerException.Message, Does.StartWith("Mock worker thread exception."));
        }

        [Test]
        public void NotifyCountMetricEventBuffered()
        {
            Boolean bufferProcessedEventTriggered = false;
            testSizeLimitedBufferProcessor.BufferProcessed += (object sender, EventArgs e) => { bufferProcessedEventTriggered = true; };
            testSizeLimitedBufferProcessor.Start();
            testSizeLimitedBufferProcessor.NotifyCountMetricEventBuffered();
            testSizeLimitedBufferProcessor.NotifyCountMetricEventBuffered();
            testSizeLimitedBufferProcessor.NotifyCountMetricEventBuffered();
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(millisecondsToWaitBeforeStop);

            Assert.IsTrue(bufferProcessedEventTriggered);
        }

        [Test]
        public void NotifyAmountMetricEventBuffered()
        {
            Boolean bufferProcessedEventTriggered = false;
            testSizeLimitedBufferProcessor.BufferProcessed += (object sender, EventArgs e) => { bufferProcessedEventTriggered = true; };
            testSizeLimitedBufferProcessor.Start();
            testSizeLimitedBufferProcessor.NotifyAmountMetricEventBuffered();
            testSizeLimitedBufferProcessor.NotifyAmountMetricEventBuffered();
            testSizeLimitedBufferProcessor.NotifyAmountMetricEventBuffered();
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(millisecondsToWaitBeforeStop);

            Assert.IsTrue(bufferProcessedEventTriggered);
        }

        [Test]
        public void NotifyStatusMetricEventBuffered()
        {
            Boolean bufferProcessedEventTriggered = false;
            testSizeLimitedBufferProcessor.BufferProcessed += (object sender, EventArgs e) => { bufferProcessedEventTriggered = true; };
            testSizeLimitedBufferProcessor.Start();
            testSizeLimitedBufferProcessor.NotifyStatusMetricEventBuffered();
            testSizeLimitedBufferProcessor.NotifyStatusMetricEventBuffered();
            testSizeLimitedBufferProcessor.NotifyStatusMetricEventBuffered();
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(millisecondsToWaitBeforeStop);

            Assert.IsTrue(bufferProcessedEventTriggered);
        }

        [Test]
        public void NotifyIntervalMetricEventBuffered()
        {
            Boolean bufferProcessedEventTriggered = false;
            testSizeLimitedBufferProcessor.BufferProcessed += (object sender, EventArgs e) => { bufferProcessedEventTriggered = true; };
            testSizeLimitedBufferProcessor.Start();
            testSizeLimitedBufferProcessor.NotifyIntervalMetricEventBuffered();
            testSizeLimitedBufferProcessor.NotifyIntervalMetricEventBuffered();
            testSizeLimitedBufferProcessor.NotifyIntervalMetricEventBuffered();
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(millisecondsToWaitBeforeStop);

            Assert.IsTrue(bufferProcessedEventTriggered);
        }
        #region Nested Classes

        /// <summary>
        /// Simple implementation of MetricLoggerBuffer which counts the number of each different type of metric event logged.
        /// </summary>
        /// <remarks>In real world use cases, classes derived from WorkerThreadBufferProcessorBase aren't instantiated and called directly, but are used in conjunction with (i.e. through) implementations of MetricLoggerBuffer.  MetricLoggerBuffer also includes serialization around calls to the WorkerThreadBufferProcessorBase.Notify*() methods which are critical to WorkerThreadBufferProcessorBase's operation.  Hence makes sense to test in the context of being used from MetricLoggerBuffer.</remarks>
        private class CountingMetricLogger : MetricLoggerBuffer
        {
            private Int32 amountMetricsProcessed;
            private Int32 countMetricsProcessed;
            private Int32 intervalMetricsProcessed;
            private Int32 statusMetricsProcessed;

            /// <summary>
            /// The number of amount metrics processed.
            /// </summary>
            public Int32 AmountMetricsProcessed
            {
                get { return amountMetricsProcessed; }
            }

            /// <summary>
            /// The number of count metrics processed.
            /// </summary>
            public Int32 CountMetricsProcessed
            {
                get { return countMetricsProcessed; }
            }

            /// <summary>
            /// The number of interval metrics processed.
            /// </summary>
            public Int32 IntervalMetricsProcessed
            {
                get { return intervalMetricsProcessed; }
            }

            /// <summary>
            /// The number of status metrics processed.
            /// </summary>
            public Int32 StatusMetricsProcessed
            {
                get { return statusMetricsProcessed; }
            }

            public CountingMetricLogger(IBufferProcessingStrategy bufferProcessingStrategy, IntervalMetricBaseTimeUnit intervalMetricBaseTimeUnit, bool intervalMetricChecking)
                : base(bufferProcessingStrategy, intervalMetricBaseTimeUnit, intervalMetricChecking)
            {
                amountMetricsProcessed = 0;
                countMetricsProcessed = 0;
                intervalMetricsProcessed = 0;
                statusMetricsProcessed = 0;
            }

            protected override void ProcessCountMetricEvents(Queue<CountMetricEventInstance> countMetricEvents)
            {
                countMetricsProcessed += countMetricEvents.Count;
            }

            protected override void ProcessAmountMetricEvents(Queue<AmountMetricEventInstance> amountMetricEvents)
            {
                amountMetricsProcessed += amountMetricEvents.Count;
            }

            protected override void ProcessStatusMetricEvents(Queue<StatusMetricEventInstance> statusMetricEvents)
            {
                statusMetricsProcessed += statusMetricEvents.Count;
            }

            protected override void ProcessIntervalMetricEvents(Queue<Tuple<IntervalMetricEventInstance, Int64>> intervalMetricEventsAndDurations)
            {
                intervalMetricsProcessed += intervalMetricEventsAndDurations.Count;
            }
        }

        #endregion
    }
}
