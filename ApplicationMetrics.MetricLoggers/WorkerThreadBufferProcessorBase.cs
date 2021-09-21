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

namespace ApplicationMetrics.MetricLoggers
{
    /// <summary>
    /// Provides common functionality for classes implementing interface IBufferProcessingStrategy, which use a worker thread to implement a buffer processing strategy.
    /// </summary>
    public abstract class WorkerThreadBufferProcessorBase : IBufferProcessingStrategy, IDisposable
    {
        /// <summary>The number of count metric events currently stored in the buffer.</summary>
        private int countMetricEventsBuffered;
        /// <summary>The number of amount metric events currently stored in the buffer.</summary>
        private int amountMetricEventsBuffered;
        /// <summary>The number of status metric events currently stored in the buffer.</summary>
        private int statusMetricEventsBuffered;
        /// <summary>The number of interval metric events currently stored in the buffer.</summary>
        private int intervalMetricEventsBuffered;
        /// <summary>Worker thread which implements the strategy to process the contents of the buffers.</summary>
        private Thread bufferProcessingWorkerThread;
        /// <summary>Set with any exception which occurrs on the worker thread.  Null if no exception has occurred.</summary>
        private Exception processingException;
        /// <summary>Whether a stop request has been received.</summary>
        protected volatile bool stopRequestReceived;
        /// <summary>Whether any metric events remaining in the buffers when the Stop() method is called should be processed.</summary>
        protected volatile bool processRemainingBufferredMetricsOnStop;
        /// <summary>Signal that will be set when the worker thread processing is complete (for unit testing).</summary>
        protected ManualResetEvent loopIterationCompleteSignal;
        /// <summary>Indicates whether the object has been disposed.</summary>
        protected bool disposed;

        /// <summary>
        /// Contains an exception which occurred on the worker thread during buffer processing.  Null if no exception has occurred.
        /// </summary>
        protected Exception ProcessingException
        {
            get { return processingException; }
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="E:ApplicationMetrics.MetricLoggers.IBufferProcessingStrategy.BufferProcessed"]/*'/>
        public event EventHandler BufferProcessed;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.WorkerThreadBufferProcessorBase class.
        /// </summary>
        public WorkerThreadBufferProcessorBase()
        {
            countMetricEventsBuffered = 0;
            amountMetricEventsBuffered = 0;
            statusMetricEventsBuffered = 0;
            intervalMetricEventsBuffered = 0;
            processingException = null;
            processRemainingBufferredMetricsOnStop = true;
            loopIterationCompleteSignal = null;
            disposed = false;
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.WorkerThreadBufferProcessorBase class.
        /// </summary>
        /// <param name="loopIterationCompleteSignal">Signal that will be set when the worker thread is complete.</param>
        public WorkerThreadBufferProcessorBase(ManualResetEvent loopIterationCompleteSignal)
            : this()
        {
            this.loopIterationCompleteSignal = loopIterationCompleteSignal;
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IBufferProcessingStrategy.Start"]/*'/>
        public virtual void Start()
        {
            if (bufferProcessingWorkerThread == null)
                throw new InvalidOperationException("Worker thread implementation has not been set.");

            stopRequestReceived = false;
            bufferProcessingWorkerThread.Name = "ApplicationMetrics.MetricLoggers.WorkerThreadBufferProcessorBase metric event buffer processing worker thread.";
            bufferProcessingWorkerThread.IsBackground = true;
            bufferProcessingWorkerThread.Start();
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IBufferProcessingStrategy.Stop"]/*'/>
        public virtual void Stop()
        {
            // Check whether any exceptions have occurred on the worker thread and re-throw
            CheckAndThrowProcessingException();
            stopRequestReceived = true;
            // Wait for the worker thread to finish
            JoinWorkerThread();
            // Check for exceptions again incase one occurred when processing the stop request
            CheckAndThrowProcessingException();
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IBufferProcessingStrategy.Stop(System.Boolean)"]/*'/>
        public virtual void Stop(bool processRemainingBufferedMetricEvents)
        {
            this.processRemainingBufferredMetricsOnStop = processRemainingBufferedMetricEvents;
            Stop();
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IBufferProcessingStrategy.NotifyCountMetricEventBuffered"]/*'/>
        public virtual void NotifyCountMetricEventBuffered()
        {
            CheckAndThrowProcessingException();
            Interlocked.Increment(ref countMetricEventsBuffered);
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IBufferProcessingStrategy.NotifyAmountMetricEventBuffered"]/*'/>
        public virtual void NotifyAmountMetricEventBuffered()
        {
            CheckAndThrowProcessingException();
            Interlocked.Increment(ref amountMetricEventsBuffered);
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IBufferProcessingStrategy.NotifyStatusMetricEventBuffered"]/*'/>
        public virtual void NotifyStatusMetricEventBuffered()
        {
            CheckAndThrowProcessingException();
            Interlocked.Increment(ref statusMetricEventsBuffered);
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IBufferProcessingStrategy.NotifyIntervalMetricEventBuffered"]/*'/>
        public virtual void NotifyIntervalMetricEventBuffered()
        {
            CheckAndThrowProcessingException();
            Interlocked.Increment(ref intervalMetricEventsBuffered);
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IBufferProcessingStrategy.NotifyCountMetricEventBufferCleared"]/*'/>
        public virtual void NotifyCountMetricEventBufferCleared()
        {
            Interlocked.Exchange(ref countMetricEventsBuffered, 0);
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IBufferProcessingStrategy.NotifyAmountMetricEventBufferCleared"]/*'/>
        public virtual void NotifyAmountMetricEventBufferCleared()
        {
            Interlocked.Exchange(ref amountMetricEventsBuffered, 0);
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IBufferProcessingStrategy.NotifyStatusMetricEventBufferCleared"]/*'/>
        public virtual void NotifyStatusMetricEventBufferCleared()
        {
            Interlocked.Exchange(ref statusMetricEventsBuffered, 0);
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IBufferProcessingStrategy.NotifyIntervalMetricEventBufferCleared"]/*'/>
        public virtual void NotifyIntervalMetricEventBufferCleared()
        {
            Interlocked.Exchange(ref intervalMetricEventsBuffered, 0);
        }

        #region Private/Protected Methods

        /// <summary>
        /// The action to execute on the worker thread that implements the buffer processing strategy.
        /// </summary>
        protected Action BufferProcessingAction
        {
            set
            {
                bufferProcessingWorkerThread = new Thread(() =>
                {
                    String exceptionMessagePrefix = "Exception occurred on buffer processing worker thread at ";

                    try
                    {
                        value.Invoke();
                    }
                    catch (Exception e)
                    {
                        var wrappedException = new Exception($"{exceptionMessagePrefix} {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz")}.", e);
                        Interlocked.Exchange(ref processingException, wrappedException);
                    }
                    // If no exception has occurred, and 'processRemainingBufferredMetricsOnStop' is set true, process any remaining metric events
                    if (processingException == null && TotalMetricEventsBufferred > 0 && processRemainingBufferredMetricsOnStop == true)
                    {
                        try
                        {
                            OnBufferProcessed(EventArgs.Empty);
                        }
                        catch (Exception e)
                        {
                            var wrappedException = new Exception($"{exceptionMessagePrefix} {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz")}.", e);
                            Interlocked.Exchange(ref processingException, wrappedException);
                        }
                    }
                    if (loopIterationCompleteSignal != null)
                    {
                        loopIterationCompleteSignal.Set();
                    }
                });
            }
        }

        /// <summary>
        /// Calls Join() on the worker thread, waiting until it terminates.
        /// </summary>
        protected void JoinWorkerThread()
        {
            if (bufferProcessingWorkerThread != null)
            {
                bufferProcessingWorkerThread.Join();
            }
        }

        /// <summary>
        /// Checks whether property 'ProcessingException' has been set, and re-throws the exception in the case that it has.
        /// </summary>
        protected void CheckAndThrowProcessingException()
        {
            if (processingException != null)
            {
                throw processingException;
            }
        }

        /// <summary>
        /// The total number of metric events currently stored across all buffers.
        /// </summary>
        /// <remarks>Note that the counter members accessed in this property may be accessed by multiple threads (i.e. the worker thread in member bufferProcessingWorkerThread and the client code in the main thread).  This property should only be read from methods which have locks around the queues in the corresponding MetricLoggerBuffer class (e.g. overrides of the virtual 'Notify' methods defined in this class, which are called from the Add(), Set(), etc... methods in the MetricLoggerBuffer class).</remarks>
        protected virtual long TotalMetricEventsBufferred
        {
            get
            {
                return countMetricEventsBuffered + amountMetricEventsBuffered + statusMetricEventsBuffered + intervalMetricEventsBuffered;
            }
        }

        /// <summary>
        /// Raises the BufferProcessed event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnBufferProcessed(EventArgs e)
        {
            if (BufferProcessed != null)
            {
                BufferProcessed(this, e);
            }
        }

        #endregion

        #region Finalize / Dispose Methods

        /// <summary>
        /// Releases the unmanaged resources used by the WorkerThreadBufferProcessorBase.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #pragma warning disable 1591
        ~WorkerThreadBufferProcessorBase()
        {
            Dispose(false);
        }
        #pragma warning restore 1591

        /// <summary>
        /// Provides a method to free unmanaged resources used by this class.
        /// </summary>
        /// <param name="disposing">Whether the method is being called as part of an explicit Dispose routine, and hence whether managed resources should also be freed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Free other state (managed objects).
                    if (loopIterationCompleteSignal != null)
                    {
                        loopIterationCompleteSignal.Dispose();
                    }
                }
                // Free your own state (unmanaged objects).

                // Set large fields to null.

                disposed = true;
            }
        }

        #endregion
    }
}
