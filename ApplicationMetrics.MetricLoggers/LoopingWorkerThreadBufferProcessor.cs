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
        // TODO: Since this class has a ManualResetEvent member, it should implement IDisposable, but since the ManualResetEvent is only used for unit tests, I'm not going to implement for now.

        private int dequeueOperationLoopInterval;
        private ManualResetEvent loopIterationCompleteSignal;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.LoopingWorkerThreadBufferProcessor class.
        /// </summary>
        /// <param name="dequeueOperationLoopInterval">The time to wait (in milliseconds) between iterations of the worker thread which dequeues and processes metric events.</param>
        public LoopingWorkerThreadBufferProcessor(int dequeueOperationLoopInterval)
            : base()
        {
            if (dequeueOperationLoopInterval < 0)
            {
                throw new ArgumentOutOfRangeException("dequeueOperationLoopInterval", dequeueOperationLoopInterval, "Argument 'dequeueOperationLoopInterval' must be greater than or equal to 0.");
            }

            this.dequeueOperationLoopInterval = dequeueOperationLoopInterval;
            loopIterationCompleteSignal = null;
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.LoopingWorkerThreadBufferProcessor class.
        /// </summary>
        /// <param name="dequeueOperationLoopInterval">The time to wait (in milliseconds) between iterations of the worker thread which dequeues and processes metric events.</param>
        /// <param name="loopIterationCompleteSignal">Signal that is set when a single iteration of the worker thread is complete (for unit testing).</param>
        public LoopingWorkerThreadBufferProcessor(int dequeueOperationLoopInterval, ManualResetEvent loopIterationCompleteSignal) 
            : base()
        {
            if (dequeueOperationLoopInterval < 0)
            {
                throw new ArgumentOutOfRangeException("dequeueOperationLoopInterval", dequeueOperationLoopInterval, "Argument 'dequeueOperationLoopInterval' must be greater than or equal to 0.");
            }

            this.dequeueOperationLoopInterval = dequeueOperationLoopInterval;
            this.loopIterationCompleteSignal = loopIterationCompleteSignal;
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IBufferProcessingStrategy.Start"]/*'/>
        public override void Start()
        {
            bufferProcessingWorkerThread = new Thread(delegate()
            {
                while (cancelRequest == false)
                {
                    OnBufferProcessed(EventArgs.Empty);
                    if (dequeueOperationLoopInterval > 0)
                    {
                        Thread.Sleep(dequeueOperationLoopInterval);
                    }
                    // If the code is being tested, allow only a single iteration of the loop
                    if (loopIterationCompleteSignal != null)
                    {
                        loopIterationCompleteSignal.Set();
                        break;
                    }
                }
                if (TotalMetricEventsBufferred > 0 && processRemainingBufferredMetricsOnStop == true)
                {
                    OnBufferProcessed(EventArgs.Empty);
                }
            });

            base.Start();
        }
    }
}
