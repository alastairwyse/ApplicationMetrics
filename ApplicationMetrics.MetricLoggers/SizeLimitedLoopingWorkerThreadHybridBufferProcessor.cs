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
using System.Diagnostics;
using System.Threading;

namespace ApplicationMetrics.MetricLoggers
{
    /// <summary>
    /// Implements a buffer processing strategy for <see cref="MetricLoggerBuffer"/> subclasses, that processes the buffers when either the total number of buffered metric events reaches a pre-defined limit or a specified looping interval expires, whichever occurs first.
    /// </summary>
    public class SizeLimitedLoopingWorkerThreadHybridBufferProcessor : SizeLimitedBufferProcessor
    {
        /// <summary>The time to wait (in milliseconds) between iterations of the worker thread which dequeues and processes metric events.</summary>
        protected Int32 dequeueOperationLoopInterval;
        /// <summary>The provider to use for the current date and time.</summary>
        protected IDateTimeProvider dateTimeProvider;
        /// <summary>Indicates whether the worker thread is currently processing the buffer contents.</summary>
        protected volatile Boolean isProcessing;
        /// <summary>Mutual exclusion lock object for member 'lastProcessingCompleteTime'.</summary>
        protected Object lastProcessingCompleteTimeLockObject;
        /// <summary>The time at which the last buffer processing completed.</summary>
        protected DateTime lastProcessingCompleteTime;
        /// <summary>Thread which loops, triggering buffer processing at specified intervals.</summary>
        protected Thread loopingTriggerThread;
        /// <summary>Signal that is waited on each time an iteration of the looping trigger thread completes (for unit testing).</summary>
        protected ManualResetEvent loopingTriggerThreadLoopCompleteSignal;
        /// <summary>The most recent interval that the looping trigger thread waited for between iterations (for unit testing).</summary>
        protected Int32 lastWaitInterval;
        
        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.SizeLimitedLoopingWorkerThreadHybridBufferProcessor class.
        /// </summary>
        /// <param name="bufferSizeLimit">The total size of the buffers which when reached, triggers processing of the buffer contents.</param>
        /// <param name="dequeueOperationLoopInterval">The time to wait (in milliseconds) between buffer processing iterations.</param>
        public SizeLimitedLoopingWorkerThreadHybridBufferProcessor(Int32 bufferSizeLimit, Int32 dequeueOperationLoopInterval)
            : base(bufferSizeLimit)
        {
            if (dequeueOperationLoopInterval < 1)
                throw new ArgumentOutOfRangeException(nameof(dequeueOperationLoopInterval), $"Parameter '{nameof(dequeueOperationLoopInterval)}' with value {dequeueOperationLoopInterval} cannot be less than 1.");

            this.dequeueOperationLoopInterval = dequeueOperationLoopInterval;
            dateTimeProvider = new StopwatchDateTimeProvider();
            isProcessing = false;
            lastProcessingCompleteTimeLockObject = new Object();
            loopingTriggerThreadLoopCompleteSignal = null;

            base.BufferProcessingAction = () =>
            {
                while (stopRequestReceived == false)
                {
                    bufferProcessSignal.WaitOne();
                    if (stopRequestReceived == false)
                    {
                        isProcessing = true;
                        OnBufferProcessed(EventArgs.Empty);
                        lock (lastProcessingCompleteTimeLockObject)
                        {
                            lastProcessingCompleteTime = dateTimeProvider.UtcNow();
                        }
                        isProcessing = false;
                        bufferProcessSignal.Reset();
                    }
                }
            };

            loopingTriggerThread = new Thread(() =>
            {
                DateTime previousLoopIterationLastProcessingCompleteTime;
                lock (lastProcessingCompleteTimeLockObject)
                {
                    previousLoopIterationLastProcessingCompleteTime = lastProcessingCompleteTime;
                }

                while (stopRequestReceived == false)
                {
                    DateTime lastProcessingCompleteTimeCopy;
                    lock (lastProcessingCompleteTimeLockObject)
                    {
                        lastProcessingCompleteTimeCopy = lastProcessingCompleteTime;
                    }
                    if (isProcessing == false)
                    {
                        if (lastProcessingCompleteTimeCopy != previousLoopIterationLastProcessingCompleteTime)
                        {
                            // Processing has occurred since the last loop iteration
                            //   Sleep for the loop interval less the time since the last processing completed
                            Int32 sleepTime = Convert.ToInt32((lastProcessingCompleteTimeCopy.AddMilliseconds(dequeueOperationLoopInterval) - dateTimeProvider.UtcNow()).TotalMilliseconds);
                            lastWaitInterval = sleepTime;
                            previousLoopIterationLastProcessingCompleteTime = lastProcessingCompleteTimeCopy;
                            if (sleepTime > 0)
                            {
                                Thread.Sleep(sleepTime);
                            }
                        }
                        else
                        {
                            // No processing occurred since the last loop iteration so trigger buffer processing
                            previousLoopIterationLastProcessingCompleteTime = lastProcessingCompleteTimeCopy;
                            bufferProcessSignal.Set();
                            lastWaitInterval = dequeueOperationLoopInterval;
                            Thread.Sleep(dequeueOperationLoopInterval);
                        }
                    }
                    else
                    {
                        // Buffers are currently being processed so sleep for the full loop interval
                        previousLoopIterationLastProcessingCompleteTime = lastProcessingCompleteTimeCopy;
                        lastWaitInterval = dequeueOperationLoopInterval;
                        Thread.Sleep(dequeueOperationLoopInterval);
                    }

                    if (loopingTriggerThreadLoopCompleteSignal != null && stopRequestReceived == false)
                    {
                        loopingTriggerThreadLoopCompleteSignal.WaitOne();
                        Thread.Sleep(250);
                    }
                }
            });
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.SizeLimitedLoopingWorkerThreadHybridBufferProcessor class.
        /// </summary>
        /// <param name="bufferSizeLimit">The total size of the buffers which when reached, triggers processing of the buffer contents.</param>
        /// <param name="dequeueOperationLoopInterval">The time to wait (in milliseconds) between buffer processing iterations.</param>
        /// <param name="dateTimeProvider">The provider to use for the current date and time.</param>
        public SizeLimitedLoopingWorkerThreadHybridBufferProcessor(Int32 bufferSizeLimit, Int32 dequeueOperationLoopInterval, IDateTimeProvider dateTimeProvider)
            : this(bufferSizeLimit, dequeueOperationLoopInterval)
        {
            this.dateTimeProvider = dateTimeProvider;
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.SizeLimitedLoopingWorkerThreadHybridBufferProcessor class.
        /// </summary>
        /// <param name="bufferSizeLimit">The total size of the buffers which when reached, triggers processing of the buffer contents.</param>
        /// <param name="dequeueOperationLoopInterval">The time to wait (in milliseconds) between buffer processing iterations.</param>
        /// <param name="dateTimeProvider">The provider to use for the current date and time.</param>
        /// <param name="loopingTriggerThreadLoopCompleteSignal">Signal that is waited on each time an iteration of the looping trigger thread completes (for unit testing).</param>
        /// <param name="workerThreadCompleteSignal">Signal that will be set when the worker thread processing is complete (for unit testing).</param>
        /// <remarks>This constructor is included to facilitate unit testing.</remarks>
        public SizeLimitedLoopingWorkerThreadHybridBufferProcessor(Int32 bufferSizeLimit, Int32 dequeueOperationLoopInterval, IDateTimeProvider dateTimeProvider, ManualResetEvent loopingTriggerThreadLoopCompleteSignal, ManualResetEvent workerThreadCompleteSignal)
            : this(bufferSizeLimit, dequeueOperationLoopInterval, dateTimeProvider)
        {
            this.loopingTriggerThreadLoopCompleteSignal = loopingTriggerThreadLoopCompleteSignal;
            base.loopIterationCompleteSignal = workerThreadCompleteSignal;
        }

        /// <inheritdoc/>
        public override void Start()
        {
            lock (lastProcessingCompleteTimeLockObject)
            {
                lastProcessingCompleteTime = dateTimeProvider.UtcNow();
            }
            base.Start();
            loopingTriggerThread.Name = $"{this.GetType().FullName} looping buffer processing trigger thread.";
            loopingTriggerThread.IsBackground = true;
            loopingTriggerThread.Start();
        }

        /// <inheritdoc/>
        public override void Stop()
        {
            base.Stop();
            loopingTriggerThread.Join();
        }

        #region Finalize / Dispose Methods

        /// <summary>
        /// Provides a method to free unmanaged resources used by this class.
        /// </summary>
        /// <param name="disposing">Whether the method is being called as part of an explicit Dispose routine, and hence whether managed resources should also be freed.</param>
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                try
                {
                    if (disposing)
                    {
                        // Free other state (managed objects).
                        if (loopingTriggerThreadLoopCompleteSignal != null)
                        {
                            loopingTriggerThreadLoopCompleteSignal.Dispose();
                        }
                    }
                    // Free your own state (unmanaged objects).

                    // Set large fields to null.
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }
        }

        #endregion
    }
}
