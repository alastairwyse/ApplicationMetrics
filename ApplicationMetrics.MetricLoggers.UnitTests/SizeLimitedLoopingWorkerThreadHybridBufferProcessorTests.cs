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
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using NUnit.Framework.Internal;
using NSubstitute;

namespace ApplicationMetrics.MetricLoggers.UnitTests
{
    /// <summary>
    /// Unit tests for class ApplicationMetrics.MetricLoggers.SizeLimitedLoopingWorkerThreadHybridBufferProcessor.
    /// </summary>
    public class SizeLimitedLoopingWorkerThreadHybridBufferProcessorTests
    {
        // Am using Thread.Sleep() statements to synchronise activity between the main thread, the buffer processing worker thread, and the looping timer thread, and hence results could be non-deterministic depending on system thread scheduling and performance.
        // Given the class under test uses 3 threads, having fully deterministic tests would mean having to create a lot of test-only event wait handles which would have to be continually checked for null in non-test operation.
        // The current implementation strikes a balance between having fully deterministic tests, and not interfering too much with normal runtime code/operation.

        private IDateTimeProvider mockDateTimeProvider;
        private ManualResetEvent loopingTriggerThreadLoopCompleteSignal;
        private ManualResetEvent workerThreadCompleteSignal;
        private EventHandler processHandler;
        private SizeLimitedLoopingWorkerThreadHybridBufferProcessorWithProtectedMethods testSizeLimitedLoopingWorkerThreadHybridBufferProcessor;
        private Int32 processEventsRaised;

        [SetUp]
        protected void SetUp()
        {
            mockDateTimeProvider = Substitute.For<IDateTimeProvider>();
            loopingTriggerThreadLoopCompleteSignal = new ManualResetEvent(false);
            workerThreadCompleteSignal = new ManualResetEvent(false);
            processHandler = (Object sender, EventArgs e) =>
            {
                processEventsRaised++;
                // The following method calls simulate resetting that occurs in the MetricLoggerBuffer.DequeueAndProcessMetricEvents() method
                testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.NotifyCountMetricEventBufferCleared();
                testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.NotifyAmountMetricEventBufferCleared();
                testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.NotifyStatusMetricEventBufferCleared();
                testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.NotifyIntervalMetricEventBufferCleared();
            };
            testSizeLimitedLoopingWorkerThreadHybridBufferProcessor = new SizeLimitedLoopingWorkerThreadHybridBufferProcessorWithProtectedMethods(3, 250, mockDateTimeProvider, loopingTriggerThreadLoopCompleteSignal, workerThreadCompleteSignal);
            testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.BufferProcessed += processHandler;
            processEventsRaised = 0;
        }

        [TearDown]
        protected void TearDown()
        {
            testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.BufferProcessed -= processHandler;
            testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.Dispose();
            workerThreadCompleteSignal.Dispose();
            loopingTriggerThreadLoopCompleteSignal.Dispose();
        }

        [Test]
        public void Constructor_DequeueOperationLoopIntervalParameterLessThan1()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                testSizeLimitedLoopingWorkerThreadHybridBufferProcessor = new SizeLimitedLoopingWorkerThreadHybridBufferProcessorWithProtectedMethods(3, 0, mockDateTimeProvider, loopingTriggerThreadLoopCompleteSignal, workerThreadCompleteSignal);
            });

            Assert.That(e.Message, Does.StartWith("Parameter 'dequeueOperationLoopInterval' with value 0 cannot be less than 1."));
            Assert.AreEqual(e.ParamName, "dequeueOperationLoopInterval");
        }

        /// <summary>
        /// Simulates where buffer processing is triggered twice by the looping trigger thread.
        /// </summary>
        [Test]
        public void Start_BufferProcessedEventsRaisedByLoopingTriggerThread()
        {
            mockDateTimeProvider.UtcNow().Returns<DateTime>
            (
                // This is the call to UtcNow() from the Start() method
                CreateDataTimeFromString("2025-05-02 18:13:00.000"),
                // Simulates the call to UtcNow() at the end of the first buffer processing, triggered by the first iteration of the looping trigger thread
                CreateDataTimeFromString("2025-05-02 18:13:00.100"),
                // Simulates the call to UtcNow() from the looping trigger thread to calculate the sleep time on the second loop iteration
                CreateDataTimeFromString("2025-05-02 18:13:00.250"),
                // Simulates the call to UtcNow() at the end of the second buffer processing, triggered by the third iteration of the looping trigger thread
                CreateDataTimeFromString("2025-05-02 18:13:00.350")
            );

            testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.Start();
            // Wait for all threads to start and for the looping trigger thread to complete one iteration
            Thread.Sleep(500);
            // Should have slept for the full 'dequeueOperationLoopInterval' after signalling the buffer processing worker thread
            Assert.AreEqual(250, testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.LastWaitInterval);
            // Signal the looping trigger thread and wait for another iteration
            loopingTriggerThreadLoopCompleteSignal.Set();
            Thread.Sleep(500);
            // 'lastProcessingCompleteTime' should be the same as for the previous loop iteration, hence should have slept for just 100ms and NOT signalled the buffer processing worker thread
            Assert.AreEqual(100, testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.LastWaitInterval);
            // Signal the looping trigger thread and wait for another iteration
            loopingTriggerThreadLoopCompleteSignal.Set();
            Thread.Sleep(500);
            // Should have again slept for the full 'dequeueOperationLoopInterval' after signalling the buffer processing worker thread
            Assert.AreEqual(250, testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.LastWaitInterval);
            // 250ms wait after being signalled will mean that 'stopRequestReceived' will be true on the next looping trigger thread iteration, due to below call to Stop()
            loopingTriggerThreadLoopCompleteSignal.Set();
            testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.Stop();
            workerThreadCompleteSignal.WaitOne();

            Assert.AreEqual(2, processEventsRaised);
            mockDateTimeProvider.Received(4).UtcNow();
        }

        /// <summary>
        /// Simulates where buffer processing is triggered twice by the looping trigger thread, during which the sleep time is calculated as less than 0.
        /// </summary>
        [Test]
        public void Start_LoopingTriggerThreadSleepTimeLessThan0()
        {
            mockDateTimeProvider.UtcNow().Returns<DateTime>
            (
                // This is the call to UtcNow() from the Start() method
                CreateDataTimeFromString("2025-05-02 18:13:00.000"),
                // Simulates the call to UtcNow() at the end of the first buffer processing, triggered by the first iteration of the looping trigger thread
                CreateDataTimeFromString("2025-05-02 18:13:00.100"),
                // Simulates the call to UtcNow() from the looping trigger thread to calculate the sleep time on the second loop iteration
                CreateDataTimeFromString("2025-05-02 18:13:00.450"),
                // Simulates the call to UtcNow() at the end of the second buffer processing, triggered by the third iteration of the looping trigger thread
                CreateDataTimeFromString("2025-05-02 18:13:00.500")
            );

            testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.Start();
            // Wait for all threads to start and for the looping trigger thread to complete one iteration
            Thread.Sleep(500);
            // Should have slept for the full 'dequeueOperationLoopInterval' after signalling the buffer processing worker thread
            Assert.AreEqual(250, testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.LastWaitInterval);
            // Signal the looping trigger thread and wait for another iteration
            loopingTriggerThreadLoopCompleteSignal.Set();
            Thread.Sleep(500);
            // 'lastProcessingCompleteTime' should be the same as the previous loop iteration, and UtcNow() gives a time 100ms later than 'lastProcessingCompleteTime' plus the 250ms loop interval, hence 'lastWaitInterval' should be -100
            Assert.AreEqual(-100, testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.LastWaitInterval);
            // Signal the looping trigger thread and wait for another iteration
            loopingTriggerThreadLoopCompleteSignal.Set();
            Thread.Sleep(500);
            // Should have again slept for the full 'dequeueOperationLoopInterval' after signalling the buffer processing worker thread
            Assert.AreEqual(250, testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.LastWaitInterval);
            // 250ms wait after being signalled will mean that 'stopRequestReceived' will be true on the next looping trigger thread iteration, due to below call to Stop()
            loopingTriggerThreadLoopCompleteSignal.Set();
            testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.Stop();
            workerThreadCompleteSignal.WaitOne();

            Assert.AreEqual(2, processEventsRaised);
            mockDateTimeProvider.Received(4).UtcNow();
        }

        /// <summary>
        /// Tests that the looping trigger thread waits without triggering buffer processing if it iterates while processing is in progress.
        /// </summary>
        [Test]
        public void Start_LoopingTriggerThreadIteratesWhileProcessingIsOccurring()
        {
            var processingCompleteSignal = new AutoResetEvent(false);
            testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.BufferProcessed -= processHandler;
            processHandler = (Object sender, EventArgs e) =>
            {
                processEventsRaised++;
                Thread.Sleep(250);
                // The following method calls simulate resetting that occurs in the MetricLoggerBuffer.DequeueAndProcessMetricEvents() method
                testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.NotifyCountMetricEventBufferCleared();
                testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.NotifyAmountMetricEventBufferCleared();
                testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.NotifyStatusMetricEventBufferCleared();
                testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.NotifyIntervalMetricEventBufferCleared();
                processingCompleteSignal.WaitOne();
            };
            testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.BufferProcessed += processHandler;

            mockDateTimeProvider.UtcNow().Returns<DateTime>
            (
                // This is the call to UtcNow() from the Start() method
                CreateDataTimeFromString("2025-05-02 18:13:00.000"),
                // Simulates the call to UtcNow() at the end of the first buffer processing, triggered by the first iteration of the looping trigger thread
                CreateDataTimeFromString("2025-05-02 18:13:00.100"),
                // Simulates the call to UtcNow() at the end of the second buffer processing, triggered by buffering of metric events
                CreateDataTimeFromString("2025-05-02 18:13:00.300")
            );

            testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.Start();
            // Wait for all threads to start and for the looping trigger thread to complete one iteration
            Thread.Sleep(500);
            // Signal the processing to complete
            processingCompleteSignal.Set();
            Thread.Sleep(250);
            // Should have slept for the full 'dequeueOperationLoopInterval' after signalling the buffer processing worker thread
            Assert.AreEqual(250, testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.LastWaitInterval);
            // Generate some metric events to trigger 'size limit' processing
            testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.NotifyIntervalMetricEventBuffered();
            testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.NotifyAmountMetricEventBuffered();
            testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.NotifyIntervalMetricEventBuffered();
            // Sleep to try to ensure the worker thread has enough time to process the above buffered metric events
            Thread.Sleep(250);
            // The buffer processing worker thread should now be waiting on reset event 'processingCompleteSignal', so signal another iteration of the looping trigger thread
            loopingTriggerThreadLoopCompleteSignal.Set();
            Thread.Sleep(500);
            // Should have slept for the full 'dequeueOperationLoopInterval' since member 'isProcessing' is true
            Assert.AreEqual(250, testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.LastWaitInterval);
            processingCompleteSignal.Set();
            // 250ms wait after being signalled will mean that 'stopRequestReceived' will be true on the next looping trigger thread iteration, due to below call to Stop()
            loopingTriggerThreadLoopCompleteSignal.Set();
            testSizeLimitedLoopingWorkerThreadHybridBufferProcessor.Stop();
            workerThreadCompleteSignal.WaitOne();

            Assert.AreEqual(2, processEventsRaised);
            mockDateTimeProvider.Received(3).UtcNow();
        }

        #region Private/Protected Methods

        /// <summary>
        /// Creates a DateTime from the specified yyyy-MM-dd HH:mm:ss.fff format string.
        /// </summary>
        /// <param name="stringifiedDateTime">The stringified date/time to convert.</param>
        /// <returns>A DateTime.</returns>
        protected DateTime CreateDataTimeFromString(String stringifiedDateTime)
        {
            DateTime returnDateTime = DateTime.ParseExact(stringifiedDateTime, "yyyy-MM-dd HH:mm:ss.fff", DateTimeFormatInfo.InvariantInfo);

            return DateTime.SpecifyKind(returnDateTime, DateTimeKind.Utc);
        }

        #endregion

        #region Nested Classes

        /// <summary>
        /// Version of the SizeLimitedLoopingWorkerThreadHybridBufferProcessor class where private and protected methods are exposed as public so that they can be unit tested.
        /// </summary>
        private class SizeLimitedLoopingWorkerThreadHybridBufferProcessorWithProtectedMethods : SizeLimitedLoopingWorkerThreadHybridBufferProcessor
        {
            /// <summary>
            /// The most recent interval that the looping trigger thread waited for between iterations.
            /// </summary>
            public Int32 LastWaitInterval
            {
                get { return lastWaitInterval; }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.UnitTests.SizeLimitedLoopingWorkerThreadHybridBufferProcessorTests+SizeLimitedLoopingWorkerThreadHybridBufferProcessorWithProtectedMethods class.
            /// </summary>
            /// <param name="bufferSizeLimit">The total size of the buffers which when reached, triggers processing of the buffer contents.</param>
            /// <param name="dequeueOperationLoopInterval">The time to wait (in milliseconds) between buffer processing iterations.</param>
            /// <param name="dateTimeProvider">The provider to use for the current date and time.</param>
            /// <param name="loopingTriggerThreadLoopCompleteSignal">Signal that is waited on each time an iteration of the looping trigger thread completes (for unit testing).</param>
            /// <param name="workerThreadCompleteSignal">Signal that will be set when the worker thread processing is complete (for unit testing).</param>
            public SizeLimitedLoopingWorkerThreadHybridBufferProcessorWithProtectedMethods(Int32 bufferSizeLimit, Int32 dequeueOperationLoopInterval, IDateTimeProvider dateTimeProvider, ManualResetEvent loopingTriggerThreadLoopCompleteSignal, ManualResetEvent workerThreadCompleteSignal)
                : base(bufferSizeLimit, dequeueOperationLoopInterval, dateTimeProvider, loopingTriggerThreadLoopCompleteSignal, workerThreadCompleteSignal)
            {
            }
        }

        #endregion
    }
}
