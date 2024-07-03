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
    /// Implements a buffer processing strategy for MetricLoggerBuffer classes, using a worker thread which dequeues and processes buffered metric events at a regular interval.
    /// </summary>
    public class LoopingWorkerThreadBufferProcessor : WorkerThreadBufferProcessorBase
    {
        /// <summary>The time to wait (in milliseconds) between iterations of the worker thread which dequeues and processes metric events.</summary>
        protected Int32 dequeueOperationLoopInterval;
        /// <summary>The number of iterations of the worker thread to process.</summary>
        protected Int32 loopIterationCount;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.LoopingWorkerThreadBufferProcessor class.
        /// </summary>
        /// <param name="dequeueOperationLoopInterval">The time to wait (in milliseconds) between iterations of the worker thread which dequeues and processes metric events.</param>
        public LoopingWorkerThreadBufferProcessor(Int32 dequeueOperationLoopInterval)
            : base()
        {
            if (dequeueOperationLoopInterval < 1)
                throw new ArgumentOutOfRangeException("dequeueOperationLoopInterval", dequeueOperationLoopInterval, $"Argument '{nameof(dequeueOperationLoopInterval)}' must be greater than or equal to 1.");

            this.dequeueOperationLoopInterval = dequeueOperationLoopInterval;
            loopIterationCompleteSignal = null;
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.LoopingWorkerThreadBufferProcessor class.
        /// </summary>
        /// <param name="dequeueOperationLoopInterval">The time to wait (in milliseconds) between iterations of the worker thread which dequeues and processes metric events.</param>
        /// <param name="bufferProcessingExceptionAction">An action to invoke if an error occurs during buffer processing.  Accepts a single parameter which is the <see cref="Exception"/> containing details of the error.</param>
        /// <param name="rethrowBufferProcessingException">Whether exceptions encountered during buffer processing should be rethrown when the next metric is logged.</param>
        public LoopingWorkerThreadBufferProcessor(Int32 dequeueOperationLoopInterval, Action<Exception> bufferProcessingExceptionAction, bool rethrowBufferProcessingException)
            : base(bufferProcessingExceptionAction, rethrowBufferProcessingException)
        {
            if (dequeueOperationLoopInterval < 1)
                throw new ArgumentOutOfRangeException("dequeueOperationLoopInterval", dequeueOperationLoopInterval, $"Argument '{nameof(dequeueOperationLoopInterval)}' must be greater than or equal to 1.");

            this.dequeueOperationLoopInterval = dequeueOperationLoopInterval;
            loopIterationCompleteSignal = null;
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.LoopingWorkerThreadBufferProcessor class.
        /// </summary>
        /// <param name="dequeueOperationLoopInterval">The time to wait (in milliseconds) between iterations of the worker thread which dequeues and processes metric events.</param>
        /// <param name="bufferProcessingExceptionAction">An action to invoke if an error occurs during buffer processing.  Accepts a single parameter which is the <see cref="Exception"/> containing details of the error.</param>
        /// <param name="rethrowBufferProcessingException">Whether exceptions encountered during buffer processing should be rethrown when the next metric is logged.</param>
        /// <param name="loopIterationCompleteSignal">Signal that will be set when the worker thread processing is complete (for unit testing).</param>
        /// <param name="loopIterationCount">The number of iterations of the worker thread to process.</param>
        /// <remarks>This constructor is included to facilitate unit testing.</remarks>
        public LoopingWorkerThreadBufferProcessor(Int32 dequeueOperationLoopInterval, Action<Exception> bufferProcessingExceptionAction, bool rethrowBufferProcessingException, ManualResetEvent loopIterationCompleteSignal, Int32 loopIterationCount) 
            : this(dequeueOperationLoopInterval, bufferProcessingExceptionAction, rethrowBufferProcessingException)
        {
            if (loopIterationCompleteSignal == null)
                throw new ArgumentNullException(nameof(loopIterationCompleteSignal), $"Parameter '{nameof(loopIterationCompleteSignal)}' cannot be null.");
            if (loopIterationCount < 1)
                throw new ArgumentOutOfRangeException(nameof(loopIterationCount), $"Parameter '{nameof(loopIterationCount)}' must be greater than 0.");

            base.loopIterationCompleteSignal = loopIterationCompleteSignal;
            this.loopIterationCount = loopIterationCount;
        }

        /// <inheritdoc/>
        public override void Start()
        {
            base.BufferProcessingAction = () =>
            {
                while (stopRequestReceived == false)
                {
                    OnBufferProcessed(EventArgs.Empty);
                    if (dequeueOperationLoopInterval > 0)
                    {
                        Thread.Sleep(dequeueOperationLoopInterval);
                    }
                    // If the code is being tested, break out of processing after the specified number of iterations
                    if (loopIterationCompleteSignal != null)
                    {
                        loopIterationCount--;
                        if (loopIterationCount == 0)
                        {
                            break;
                        }
                    }
                }
            };
            base.Start();
        }
    }
}
