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

using System;
using System.Threading;

namespace ApplicationMetrics.MetricLoggers
{
    /// <summary>
    /// Implements a buffer processing strategy for MetricLoggerBuffer classes, whereby when the total size of the buffers reaches a defined limit, a worker thread is signaled to process the buffers.
    /// </summary>
    public class SizeLimitedBufferProcessor : WorkerThreadBufferProcessorBase
    {
        /// <summary>The total size of the buffers which when reached, triggers processing of the buffer contents.</summary>
        protected Int32 bufferSizeLimit;
        /// <summary>Signal which is used to trigger the worker thread when the specified number of metric events are bufferred.</summary>
        protected AutoResetEvent bufferProcessSignal;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.SizeLimitedBufferProcessor class.
        /// </summary>
        /// <param name="bufferSizeLimit">The total size of the buffers which when reached, triggers processing of the buffer contents.</param>
        public SizeLimitedBufferProcessor(Int32 bufferSizeLimit)
            : base()
        {
            if (bufferSizeLimit < 1)
                throw new ArgumentOutOfRangeException(nameof(bufferSizeLimit), $"Parameter '{nameof(bufferSizeLimit)}' with value {bufferSizeLimit} cannot be less than 1.");

            base.BufferProcessingAction = () =>
            {
                while (stopRequestReceived == false)
                {
                    bufferProcessSignal.WaitOne();
                    if (stopRequestReceived == false)
                    {
                        OnBufferProcessed(EventArgs.Empty);
                    }
                    bufferProcessSignal.Reset();
                }
            };
            this.bufferSizeLimit = bufferSizeLimit;
            bufferProcessSignal = new AutoResetEvent(false);
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.SizeLimitedBufferProcessor class.
        /// </summary>
        /// <param name="bufferSizeLimit">The total size of the buffers which when reached, triggers processing of the buffer contents.</param>
        /// <param name="loopIterationCompleteSignal">Signal that will be set when the worker thread processing is complete (for unit testing).</param>
        /// <remarks>This constructor is included to facilitate unit testing.</remarks>
        public SizeLimitedBufferProcessor(Int32 bufferSizeLimit, ManualResetEvent loopIterationCompleteSignal)
            : this(bufferSizeLimit)
        {
            if (loopIterationCompleteSignal == null)
                throw new ArgumentNullException(nameof(loopIterationCompleteSignal), $"Parameter '{nameof(loopIterationCompleteSignal)}' cannot be null.");

            base.loopIterationCompleteSignal = loopIterationCompleteSignal;
        }

        /// <inheritdoc/>
        public override void Stop()
        {
            // Check whether any exceptions have occurred on the worker thread and re-throw
            CheckAndThrowProcessingException();
            stopRequestReceived = true;
            // Signal the worked thread to start processing
            bufferProcessSignal.Set();
            // Wait for the worker thread to finish
            JoinWorkerThread();
            // Check for exceptions again incase one occurred when processing the stop request
            CheckAndThrowProcessingException();
        }

        /// <inheritdoc/>
        public override void NotifyCountMetricEventBuffered()
        {
            base.NotifyCountMetricEventBuffered();
            CheckBufferLimitReached();
        }

        /// <inheritdoc/>
        public override void NotifyAmountMetricEventBuffered()
        {
            base.NotifyAmountMetricEventBuffered();
            CheckBufferLimitReached();
        }

        /// <inheritdoc/>
        public override void NotifyStatusMetricEventBuffered()
        {
            base.NotifyStatusMetricEventBuffered();
            CheckBufferLimitReached();
        }

        /// <inheritdoc/>
        public override void NotifyIntervalMetricEventBuffered()
        {
            base.NotifyIntervalMetricEventBuffered();
            CheckBufferLimitReached();
        }

        #region Private Methods

        /// <summary>
        /// Checks whether the size limit for the buffers has been reached, and if so, signals the worker thread to process the buffers.
        /// </summary>
        private void CheckBufferLimitReached()
        {
            if (TotalMetricEventsBufferred >= bufferSizeLimit)
            {
                bufferProcessSignal.Set();
            }
        }

        #endregion

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
                        bufferProcessSignal.Dispose();
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
