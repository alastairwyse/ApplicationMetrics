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
using NUnit.Framework;

namespace ApplicationMetrics.MetricLoggers.UnitTests
{
    /// <summary>
    /// Unit tests for class ApplicationMetrics.MetricLoggers.WorkerThreadBufferProcessorBase.
    /// </summary>
    /// <remarks>Since WorkerThreadBufferProcessorBase is an abstract class, tests are performed via derived classes LoopingWorkerThreadBufferProcessor, and others.</remarks>
    public class WorkerThreadBufferProcessorBaseTests
    {
        // Some of these tests use Thread.Sleep() statements to synchronise activity between the main thread and buffer processing worker thread, and hence results could be non-deterministic depending on system thread scheduling and performance.
        // Decided to do this, as making things fully deterministic would involve adding more test-only thread synchronising mechanisms (in addition to the existing WorkerThreadBufferProcessorBase.loopIterationCompleteSignal property), which would mean more redundtant statements executing during normal runtime.
        // I think the current implementation strikes a balance between having fully deterministic tests, and not interfering too much with normal runtime operation.

        private Exception testBufferProcessingException;
        private Int32 bufferProcessingExceptionActionCallCount;
        private Action<Exception> testBufferProcessingExceptionAction;
        private ManualResetEvent loopIterationCompleteSignal;
        private LoopingWorkerThreadBufferProcessor testLoopingWorkerThreadBufferProcessor;
        private ManualResetEvent waitWhenProcessingLimitReachedSignal;
        private TestBufferProcessor testBufferProcessor;

        [SetUp]
        protected void SetUp()
        {
            testBufferProcessingException = null;
            bufferProcessingExceptionActionCallCount = 0;
            testBufferProcessingExceptionAction = (Exception bufferProcessingException) =>
            {
                testBufferProcessingException = bufferProcessingException;
                bufferProcessingExceptionActionCallCount++;
            };
            loopIterationCompleteSignal = new ManualResetEvent(false);
            testLoopingWorkerThreadBufferProcessor = new LoopingWorkerThreadBufferProcessor(100, testBufferProcessingExceptionAction, true, loopIterationCompleteSignal, 5);
            waitWhenProcessingLimitReachedSignal = new ManualResetEvent(false);
            testBufferProcessor = new TestBufferProcessor(testBufferProcessingExceptionAction, true, 50, 3, waitWhenProcessingLimitReachedSignal, loopIterationCompleteSignal);
        }

        [TearDown]
        protected void TearDown()
        {
            testBufferProcessor.Dispose();
            waitWhenProcessingLimitReachedSignal.Dispose();
            testLoopingWorkerThreadBufferProcessor.Dispose();
            loopIterationCompleteSignal.Dispose();
        }

        [Test]
        public void Start_WorkerThreadImplementationNotSet()
        {
            var testBufferProcessorWithNoWorkerThreadImplementation = new BufferProcessorWithNoWorkerThreadImplementation();

            InvalidOperationException e = Assert.Throws<InvalidOperationException>(delegate
            {
                testBufferProcessorWithNoWorkerThreadImplementation.Start();
            });

            Assert.That(e.Message, Does.StartWith("Worker thread implementation has not been set."));
        }

        [Test]
        public void NotifyCountMetricEventBuffered_ExceptionOccursOnWorkerThread()
        {
            var mockException = new Exception("Mock worker thread exception.");
            testLoopingWorkerThreadBufferProcessor.BufferProcessed += (object sender, EventArgs e) => { throw mockException; };

            testLoopingWorkerThreadBufferProcessor.Start();
            loopIterationCompleteSignal.WaitOne();

            Exception e = Assert.Throws<Exception>(delegate
            {
                testLoopingWorkerThreadBufferProcessor.NotifyCountMetricEventBuffered();
            });

            Assert.That(e.Message, Does.StartWith("Exception occurred on buffer processing worker thread at "));
            Assert.That(e.InnerException.Message, Does.StartWith("Mock worker thread exception.")); 
            Assert.AreSame(testBufferProcessingException.InnerException, mockException);
            Assert.AreEqual(1, bufferProcessingExceptionActionCallCount);
        }

        [Test]
        public void NotifyAmountMetricEventBuffered_ExceptionOccursOnWorkerThread()
        {
            var mockException = new Exception("Mock worker thread exception.");
            testLoopingWorkerThreadBufferProcessor.BufferProcessed += (object sender, EventArgs e) => { throw mockException; };

            testLoopingWorkerThreadBufferProcessor.Start();
            loopIterationCompleteSignal.WaitOne();

            Exception e = Assert.Throws<Exception>(delegate
            {
                testLoopingWorkerThreadBufferProcessor.NotifyAmountMetricEventBuffered();
            });

            Assert.That(e.Message, Does.StartWith("Exception occurred on buffer processing worker thread at "));
            Assert.That(e.InnerException.Message, Does.StartWith("Mock worker thread exception."));
            Assert.AreSame(testBufferProcessingException.InnerException, mockException);
            Assert.AreEqual(1, bufferProcessingExceptionActionCallCount);
        }

        [Test]
        public void NotifyStatusMetricEventBuffered_ExceptionOccursOnWorkerThread()
        {
            var mockException = new Exception("Mock worker thread exception.");
            testLoopingWorkerThreadBufferProcessor.BufferProcessed += (object sender, EventArgs e) => { throw mockException; };

            testLoopingWorkerThreadBufferProcessor.Start();
            loopIterationCompleteSignal.WaitOne();

            Exception e = Assert.Throws<Exception>(delegate
            {
                testLoopingWorkerThreadBufferProcessor.NotifyStatusMetricEventBuffered();
            });

            Assert.That(e.Message, Does.StartWith("Exception occurred on buffer processing worker thread at "));
            Assert.That(e.InnerException.Message, Does.StartWith("Mock worker thread exception."));
            Assert.AreSame(testBufferProcessingException.InnerException, mockException);
            Assert.AreEqual(1, bufferProcessingExceptionActionCallCount);
        }

        [Test]
        public void NotifyIntervalMetricEventBuffered_ExceptionOccursOnWorkerThread()
        {
            var mockException = new Exception("Mock worker thread exception.");
            testLoopingWorkerThreadBufferProcessor.BufferProcessed += (object sender, EventArgs e) => { throw mockException; };

            testLoopingWorkerThreadBufferProcessor.Start();
            loopIterationCompleteSignal.WaitOne();

            Exception e = Assert.Throws<Exception>(delegate
            {
                testLoopingWorkerThreadBufferProcessor.NotifyIntervalMetricEventBuffered();
            });

            Assert.That(e.Message, Does.StartWith("Exception occurred on buffer processing worker thread at "));
            Assert.That(e.InnerException.Message, Does.StartWith("Mock worker thread exception."));
            Assert.AreSame(testBufferProcessingException.InnerException, mockException);
            Assert.AreEqual(1, bufferProcessingExceptionActionCallCount);
        }

        [Test]
        public void Stop_NoRemainingEventsAndProcessRemainingEventsSetTrue()
        {
            testBufferProcessor.Start();
            testBufferProcessor.NotifyCountMetricEventBuffered();
            testBufferProcessor.NotifyAmountMetricEventBuffered();
            testBufferProcessor.NotifyStatusMetricEventBuffered();
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(500);
            waitWhenProcessingLimitReachedSignal.Set();
            testBufferProcessor.Stop();
            loopIterationCompleteSignal.WaitOne();

            Assert.AreEqual(3, testBufferProcessor.EventsProcessedBeforeWaitingCount);
            Assert.AreEqual(0, testBufferProcessor.EventsProcessedAfterWaitingCount);
        }

        [Test]
        public void Stop_NoRemainingEventsAndProcessRemainingEventsSetFalse()
        {
            testBufferProcessor.Start();
            testBufferProcessor.NotifyCountMetricEventBuffered();
            testBufferProcessor.NotifyAmountMetricEventBuffered();
            testBufferProcessor.NotifyStatusMetricEventBuffered();
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(500);
            waitWhenProcessingLimitReachedSignal.Set();
            testBufferProcessor.Stop(false);
            loopIterationCompleteSignal.WaitOne();

            Assert.AreEqual(3, testBufferProcessor.EventsProcessedBeforeWaitingCount);
            Assert.AreEqual(0, testBufferProcessor.EventsProcessedAfterWaitingCount);
        }

        [Test]
        public void Stop_RemainingEventsAndProcessRemainingEventsSetTrue()
        {
            // TODO: This test is really exercising logic in the TestBufferProcessor class 'BufferProcessed' event handler.
            //   COuld probably remove this test.
            
            testBufferProcessor.Start();
            testBufferProcessor.NotifyCountMetricEventBuffered();
            testBufferProcessor.NotifyAmountMetricEventBuffered();
            testBufferProcessor.NotifyStatusMetricEventBuffered();
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(500);
            testBufferProcessor.NotifyCountMetricEventBuffered();
            testBufferProcessor.NotifyAmountMetricEventBuffered();
            waitWhenProcessingLimitReachedSignal.Set();
            testBufferProcessor.Stop();
            loopIterationCompleteSignal.WaitOne();

            Assert.AreEqual(3, testBufferProcessor.EventsProcessedBeforeWaitingCount);
            Assert.AreEqual(2, testBufferProcessor.EventsProcessedAfterWaitingCount);
        }

        [Test]
        public void Stop_RemainingEventsAndProcessRemainingEventsSetFalse()
        {
            testBufferProcessor.Start();
            testBufferProcessor.NotifyCountMetricEventBuffered();
            testBufferProcessor.NotifyAmountMetricEventBuffered();
            testBufferProcessor.NotifyStatusMetricEventBuffered();
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(500); 
            testBufferProcessor.NotifyCountMetricEventBuffered();
            testBufferProcessor.NotifyAmountMetricEventBuffered();
            waitWhenProcessingLimitReachedSignal.Set();
            testBufferProcessor.Stop(false);
            loopIterationCompleteSignal.WaitOne();

            Assert.AreEqual(3, testBufferProcessor.EventsProcessedBeforeWaitingCount);
            Assert.AreEqual(0, testBufferProcessor.EventsProcessedAfterWaitingCount);
        }

        [Test]
        public void Stop_ExceptionOccursOnWorkerThread()
        {
            testBufferProcessor.Start();
            testBufferProcessor.NotifyCountMetricEventBuffered();
            testBufferProcessor.NotifyAmountMetricEventBuffered();
            testBufferProcessor.ThrowException = true;
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(500);

            Exception e = Assert.Throws<Exception>(delegate
            {
                testBufferProcessor.Stop(false);
            });

            Assert.That(e.Message, Does.StartWith("Exception occurred on buffer processing worker thread at "));
            Assert.That(e.InnerException.Message, Does.StartWith("Mock worker thread exception."));
        }

        [Test]
        public void Stop_ExceptionOccursOnWorkerThreadProcessingRemainingEvents()
        {
            testBufferProcessor.Start();
            testBufferProcessor.NotifyCountMetricEventBuffered();
            testBufferProcessor.NotifyAmountMetricEventBuffered();
            testBufferProcessor.NotifyStatusMetricEventBuffered();
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(500);
            testBufferProcessor.NotifyAmountMetricEventBuffered();
            testBufferProcessor.NotifyStatusMetricEventBuffered();
            testBufferProcessor.ThrowException = true;
            waitWhenProcessingLimitReachedSignal.Set();

            Exception e = Assert.Throws<Exception>(delegate
            {
                testBufferProcessor.Stop();
            });

            Assert.That(e.Message, Does.StartWith("Exception occurred on buffer processing worker thread at "));
            Assert.That(e.InnerException.Message, Does.StartWith("Mock worker thread exception."));
        }

        [Test]
        public void Stop_ExceptionOccursOnWorkerThreadProcessingRemainingEventsAndRethrowBufferProcessingExceptionSetFalse()
        {
            testBufferProcessor.Dispose();
            loopIterationCompleteSignal = new ManualResetEvent(false);
            testBufferProcessor = new TestBufferProcessor(testBufferProcessingExceptionAction, false, 50, 3, waitWhenProcessingLimitReachedSignal, loopIterationCompleteSignal);
            testBufferProcessor.Start();
            testBufferProcessor.NotifyCountMetricEventBuffered();
            testBufferProcessor.NotifyAmountMetricEventBuffered();
            testBufferProcessor.NotifyStatusMetricEventBuffered();
            // Sleep to try to ensure the worker thread has enough time to process the above buffered events
            Thread.Sleep(500);
            testBufferProcessor.NotifyAmountMetricEventBuffered();
            testBufferProcessor.NotifyStatusMetricEventBuffered();
            testBufferProcessor.ThrowException = true;
            waitWhenProcessingLimitReachedSignal.Set();

            testBufferProcessor.Stop();

            Assert.That(testBufferProcessingException.InnerException.Message, Does.StartWith("Mock worker thread exception."));
            Assert.AreEqual(1, bufferProcessingExceptionActionCallCount);
        }

        [Test]
        public void BufferProcessed_ExceptionOccursOnWorkerThreadAndRethrowBufferProcessingExceptionSetFalse()
        {
            testLoopingWorkerThreadBufferProcessor.Dispose();
            loopIterationCompleteSignal = new ManualResetEvent(false);
            testLoopingWorkerThreadBufferProcessor = new LoopingWorkerThreadBufferProcessor(100, testBufferProcessingExceptionAction, false, loopIterationCompleteSignal, 5);
            var mockException = new Exception("Mock worker thread exception.");
            testLoopingWorkerThreadBufferProcessor.BufferProcessed += (object sender, EventArgs e) => { throw mockException; };

            testLoopingWorkerThreadBufferProcessor.Start();
            loopIterationCompleteSignal.WaitOne();

            testLoopingWorkerThreadBufferProcessor.NotifyIntervalMetricEventBuffered();

            Assert.AreSame(testBufferProcessingException.InnerException, mockException);
            Assert.AreEqual(1, bufferProcessingExceptionActionCallCount);
        }

        #region Nested Classes

        /// <summary>
        /// Implementation of WorkerThreadBufferProcessorBase with no worker thread implementation.
        /// </summary>
        private class BufferProcessorWithNoWorkerThreadImplementation : WorkerThreadBufferProcessorBase
        {
            public BufferProcessorWithNoWorkerThreadImplementation() 
                : base()
            {
            }
        }

        /// <summary>
        /// Implementation of WorkerThreadBufferProcessorBase where the timing of processing events and throwing exceptions can be controlled, to facilitate unit testing.
        /// </summary>
        private class TestBufferProcessor : WorkerThreadBufferProcessorBase
        {
            /// <summary>The time to wait (in milliseconds) between iterations of the worker thread.</summary>
            protected Int32 bufferProcessingLoopInterval;
            /// <summary>The number of events which were processed before the worker thread reached the specified 'events processed' limit and waited.</summary>
            protected Int32 eventsProcessedBeforeWaitingCount;
            /// <summary>The number of events which were processed after the worker thread reached the specified 'events processed' limit and waited.</summary>
            protected Int32 eventsProcessedAfterWaitingCount;
            /// <summary>The number of events the worker thread should process before pausing and waiting to be signalled.</summary>
            protected Int32 eventsProcessedBeforeWaitingLimit;
            /// <summary>The signal to wait on when the event processing limit is reached.</summary>
            protected ManualResetEvent waitWhenProcessingLimitReachedSignal;
            /// <summary>Whether the worker thread should throw an exception on its next iteration.</summary>
            protected volatile Boolean throwException;

            // Lock objects for buffer counters
            protected object countMetricEventsBufferedLock;
            protected object amountMetricEventsBufferedLock;
            protected object statusMetricEventsBufferedLock;
            protected object intervalMetricEventsBufferedLock;

            /// <summary>
            /// The number of events which were processed before the worker thread reached the specified 'events processed' limit and waited.
            /// </summary>
            public Int32 EventsProcessedBeforeWaitingCount
            {
                get { return eventsProcessedBeforeWaitingCount; }
            }

            /// <summary>
            /// The number of events which were processed after the worker thread reached the specified 'events processed' limit and waited.
            /// </summary>
            public Int32 EventsProcessedAfterWaitingCount
            {
                get { return eventsProcessedAfterWaitingCount; }
            }

            /// <summary>
            /// Whether the worker thread should throw an exception on its next iteration.
            /// </summary>
            public Boolean ThrowException
            {
                set { throwException = value; }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.UnitTests.WorkerThreadBufferProcessorBaseTests+TestBufferProcessor class.
            /// </summary>
            /// <param name="bufferProcessingExceptionAction">An action to invoke if an error occurs during buffer processing.  Accepts a single parameter which is the <see cref="Exception"/> containing details of the error.</param>
            /// <param name="rethrowBufferProcessingException">Whether exceptions encountered during buffer processing should be rethrown when the next metric is logged.</param>
            /// <param name="bufferProcessingLoopInterval">The time to wait (in milliseconds) between iterations of the worker thread.</param>
            /// <param name="eventsProcessedBeforeWaitingLimit">The number of events the worker thread should process before pausing and waiting to be signalled.</param>
            /// <param name="waitWhenProcessingLimitReachedSignal">The signal to wait on when the event processing limit is reached.</param>
            /// <param name="loopIterationCompleteSignal">The signal which is set after the buffer processing worker thread stops, to notify test code that processing is complete.</param>
            public TestBufferProcessor (Action<Exception> bufferProcessingExceptionAction, bool rethrowBufferProcessingException, Int32 bufferProcessingLoopInterval, Int32 eventsProcessedBeforeWaitingLimit, ManualResetEvent waitWhenProcessingLimitReachedSignal, ManualResetEvent loopIterationCompleteSignal)
                : base(bufferProcessingExceptionAction, rethrowBufferProcessingException, loopIterationCompleteSignal)
            {
                this.bufferProcessingLoopInterval = bufferProcessingLoopInterval;
                eventsProcessedBeforeWaitingCount = 0;
                eventsProcessedAfterWaitingCount = 0;
                this.eventsProcessedBeforeWaitingLimit = eventsProcessedBeforeWaitingLimit;
                this.waitWhenProcessingLimitReachedSignal = waitWhenProcessingLimitReachedSignal;
                throwException = false;

                countMetricEventsBufferedLock = new Object();
                amountMetricEventsBufferedLock = new Object();
                statusMetricEventsBufferedLock = new Object();
                intervalMetricEventsBufferedLock = new Object();

                base.BufferProcessed += (object sender, EventArgs e) => 
                {
                    if (throwException == true)
                        throw new Exception("Mock worker thread exception.");

                    lock (countMetricEventsBufferedLock)
                    {
                        lock (amountMetricEventsBufferedLock)
                        {
                            lock (statusMetricEventsBufferedLock)
                            {
                                lock (intervalMetricEventsBufferedLock)
                                {
                                    if (stopRequestReceived == false)
                                    {
                                        eventsProcessedBeforeWaitingCount += (Int32)base.TotalMetricEventsBufferred;
                                    }
                                    else
                                    {
                                        eventsProcessedAfterWaitingCount += (Int32)base.TotalMetricEventsBufferred;
                                    }
                                    NotifyCountMetricEventBufferCleared();
                                    NotifyAmountMetricEventBufferCleared();
                                    NotifyStatusMetricEventBufferCleared();
                                    NotifyIntervalMetricEventBufferCleared();
                                }
                            }
                        }
                    }
                };

                base.BufferProcessingAction = () =>
                {
                    while (stopRequestReceived == false)
                    {
                        if (eventsProcessedBeforeWaitingCount < eventsProcessedBeforeWaitingLimit)
                        {
                            OnBufferProcessed(EventArgs.Empty);
                            Thread.Sleep(bufferProcessingLoopInterval);
                        }
                        else
                        {
                            waitWhenProcessingLimitReachedSignal.WaitOne();
                            // Sleep here to allow a gap where the main thread can call Stop() before the next loop iteration
                            Thread.Sleep(250);
                            if (stopRequestReceived == false)
                            {
                                OnBufferProcessed(EventArgs.Empty);
                            }
                        }
                    }
                };
            }

            /// <inheritdoc/>
            public override void NotifyCountMetricEventBuffered()
            {
                lock (countMetricEventsBufferedLock)
                {
                    base.NotifyCountMetricEventBuffered();
                }
            }

            /// <inheritdoc/>
            public override void NotifyAmountMetricEventBuffered()
            {
                lock (amountMetricEventsBufferedLock)
                {
                    base.NotifyAmountMetricEventBuffered();
                }
            }

            /// <inheritdoc/>
            public override void NotifyStatusMetricEventBuffered()
            {
                lock (statusMetricEventsBufferedLock)
                {
                    base.NotifyStatusMetricEventBuffered();
                }
            }

            /// <inheritdoc/>
            public override void NotifyIntervalMetricEventBuffered()
            {
                lock (intervalMetricEventsBufferedLock)
                {
                    base.NotifyIntervalMetricEventBuffered();
                }
            }
        }

        #endregion
    }
}
