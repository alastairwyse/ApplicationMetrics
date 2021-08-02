/*
 * Copyright 2017 Alastair Wyse (https://github.com/alastairwyse/ApplicationMetrics/)
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
using StandardAbstraction;

namespace ApplicationMetrics.MetricLoggers
{
    /// <summary>
    /// Base class which acts as a buffer for implementations of interface IMetricLogger.  Stores logged metrics events in queues, so as to minimise the time taken to call the logging methods.
    /// </summary>
    /// <remarks>Derived classes must implement methods which process the buffered metric events (e.g. ProcessIntervalMetricEvent()).  These methods are called from a worker thread after dequeueing the buffered metric events.</remarks>
    abstract class MetricLoggerBuffer : IMetricLogger, IDisposable
    {
        // Queue objects 
        /// <summary>Queue used to buffer count metrics.</summary>
        protected Queue<CountMetricEventInstance> countMetricEventQueue;
        /// <summary>Queue used to buffer amount metrics.</summary>
        protected Queue<AmountMetricEventInstance> amountMetricEventQueue;
        /// <summary>Queue used to buffer status metrics.</summary>
        protected Queue<StatusMetricEventInstance> statusMetricEventQueue;
        /// <summary>Queue used to buffer interval metrics.</summary>
        protected Queue<IntervalMetricEventInstance> intervalMetricEventQueue;

        // Lock objects for queues
        /// <summary>Lock object which should be set before dequeuing from queue countMetricEventQueue.</summary>
        protected object countMetricEventQueueLock;
        /// <summary>Lock object which should be set before dequeuing from queue amountMetricEventQueue.</summary>
        protected object amountMetricEventQueueLock;
        /// <summary>Lock object which should be set before dequeuing from queue statusMetricEventQueue.</summary>
        protected object statusMetricEventQueueLock;
        /// <summary>Lock object which should be set before dequeuing from queue intervalMetricEventQueue.</summary>
        protected object intervalMetricEventQueueLock;

        /// <summary>Object which implements a processing strategy for the buffers (queues).</summary>
        protected IBufferProcessingStrategy bufferProcessingStrategy;
        /// <summary>The delegate to handle when a BufferProcessed event is raised.</summary>
        protected EventHandler bufferProcessedEventHandler;
        /// <summary>Object which provides the current date and time.</summary>
        protected IDateTime dateTime;
        /// <summary>Used to measure elapsed time since starting the buffer processor.</summary>
        protected IStopwatch stopWatch;
        /// <summary>The timestamp at which the buffer processor was started.</summary>
        protected System.DateTime startTime;
        /// <summary>Object handles any exceptions.  Allows easier unit testing by pushing exceptions to the IExceptionHandler interface.</summary>
        protected IExceptionHandler exceptionHandler;
        /// <summary>Indicates whether the object has been disposed.</summary>
        protected bool disposed;

        // Dictionary object to temporarily store the start instance of any received interval metrics
        private Dictionary<Type, IntervalMetricEventInstance> startIntervalMetricEventStore;
        // Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed(e.g.End() method called before Begin()).
        private bool intervalMetricChecking;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBuffer class.
        /// </summary>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).</param>
        protected MetricLoggerBuffer(IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking)
        {
            countMetricEventQueue = new Queue<CountMetricEventInstance>();
            amountMetricEventQueue = new Queue<AmountMetricEventInstance>();
            statusMetricEventQueue = new Queue<StatusMetricEventInstance>();
            intervalMetricEventQueue = new Queue<IntervalMetricEventInstance>();
            countMetricEventQueueLock = new object();
            amountMetricEventQueueLock = new object();
            statusMetricEventQueueLock = new object();
            intervalMetricEventQueueLock = new object();

            this.bufferProcessingStrategy = bufferProcessingStrategy;
            bufferProcessedEventHandler = delegate(object sender, EventArgs e) { DequeueAndProcessMetricEvents(); };
            this.bufferProcessingStrategy.BufferProcessed += bufferProcessedEventHandler;
            dateTime = new StandardAbstraction.DateTime();
            stopWatch = new Stopwatch();
            exceptionHandler = new ExceptionThrower();

            startIntervalMetricEventStore = new Dictionary<Type, IntervalMetricEventInstance>();
            this.intervalMetricChecking = intervalMetricChecking;
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBuffer class.  Note this is an additional constructor to facilitate unit tests, and should not be used to instantiate the class under normal conditions.
        /// </summary>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).</param>
        /// <param name="dateTime">A test (mock) DateTime object.</param>
        /// <param name="stopWatch">A test (mock) Stopwatch object.</param>
        /// <param name="exceptionHandler">A test (mock) exception handler object.</param>
        protected MetricLoggerBuffer(IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking, IDateTime dateTime, IStopwatch stopWatch, IExceptionHandler exceptionHandler)
            : this(bufferProcessingStrategy, intervalMetricChecking)
        {
            this.dateTime = dateTime;
            this.stopWatch = stopWatch;
            this.exceptionHandler = exceptionHandler;
        }

        /// <summary>
        /// Starts the buffer processing (e.g. if the implementation of the buffer processing strategy uses a worker thread, this method starts the worker thread).
        /// </summary>
        /// <remarks>This method is maintained on this class for backwards compatibility, as it is now available on interface IBufferProcessingStrategy.</remarks>
        public virtual void Start()
        {
            stopWatch.Reset();
            stopWatch.Start();
            // N.b. the idea behind populating 'startTime' is that a more accurate 'UtcNow' value can be obtained by using the stopwatch to measure the elapse time since starting and applying that offset to the value of 'startTime'.
            //   One potential issue is that we can't force the above Start() method call and below 'UtcNow' to run sequentially... e.g. OS could theoretically context switch after the Start() and cause a delay before 'startTime' is populated.
            //   This would result in the timestamps of all metrics produced by the class to be similarly offset.
            //   Have decided for now to leave this as is because...
            //     1. I can't see any way I can correct/fix it
            //     2. This class is only designed to provide ms accuracy (e.g. for interval metrics), and on modern CPUs I wouldn't expect any offsets caused by the situation outlined to be in the order of multiple milliseconds
            startTime = dateTime.UtcNow;
            bufferProcessingStrategy.Start();
        }

        /// <summary>
        /// Stops the buffer processing (e.g. if the implementation of the buffer processing strategy uses a worker thread, this method stops the worker thread).
        /// </summary>
        /// <remarks>This method is maintained on this class for backwards compatibility, as it is now available on interface IBufferProcessingStrategy.</remarks>
        public virtual void Stop()
        {
            bufferProcessingStrategy.Stop();
            stopWatch.Stop();
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IMetricLogger.Increment(ApplicationMetrics.CountMetric)"]/*'/>
        public void Increment(CountMetric countMetric)
        {
            lock (countMetricEventQueueLock)
            {
                countMetricEventQueue.Enqueue(new CountMetricEventInstance(countMetric, GetStopWatchUtcNow()));
                bufferProcessingStrategy.NotifyCountMetricEventBuffered();
            }
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IMetricLogger.Add(ApplicationMetrics.AmountMetric)"]/*'/>
        public void Add(AmountMetric amountMetric)
        {
            lock (amountMetricEventQueueLock)
            {
                amountMetricEventQueue.Enqueue(new AmountMetricEventInstance(amountMetric, GetStopWatchUtcNow()));
                bufferProcessingStrategy.NotifyAmountMetricEventBuffered();
            }
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IMetricLogger.Set(ApplicationMetrics.StatusMetric)"]/*'/>
        public void Set(StatusMetric statusMetric)
        {
            lock (statusMetricEventQueueLock)
            {
                statusMetricEventQueue.Enqueue(new StatusMetricEventInstance(statusMetric, GetStopWatchUtcNow()));
                bufferProcessingStrategy.NotifyStatusMetricEventBuffered();
            }
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IMetricLogger.Begin(ApplicationMetrics.IntervalMetric)"]/*'/>
        public void Begin(IntervalMetric intervalMetric)
        {
            lock (intervalMetricEventQueueLock)
            {
                intervalMetricEventQueue.Enqueue(new IntervalMetricEventInstance(intervalMetric, IntervalMetricEventTimePoint.Start, GetStopWatchUtcNow()));
            }
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IMetricLogger.End(ApplicationMetrics.IntervalMetric)"]/*'/>
        public void End(IntervalMetric intervalMetric)
        {
            lock (intervalMetricEventQueueLock)
            {
                intervalMetricEventQueue.Enqueue(new IntervalMetricEventInstance(intervalMetric, IntervalMetricEventTimePoint.End, GetStopWatchUtcNow()));
                bufferProcessingStrategy.NotifyIntervalMetricEventBuffered();
            }
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IMetricLogger.CancelBegin(ApplicationMetrics.IntervalMetric)"]/*'/>
        public void CancelBegin(IntervalMetric intervalMetric)
        {
            lock (intervalMetricEventQueueLock)
            {
                intervalMetricEventQueue.Enqueue(new IntervalMetricEventInstance(intervalMetric, IntervalMetricEventTimePoint.Cancel, GetStopWatchUtcNow()));
            }
        }

        #region Abstract Methods

        /// <summary>
        /// Processes a logged count metric event.
        /// </summary>
        /// <param name="countMetricEvent">The count metric event to process.</param>
        /// <remarks>Implementations of this method define how a count metric event should be processed after it has been retrieved from the internal buffer queue.  The event could for example be written to a database, or to the console.</remarks>
        protected abstract void ProcessCountMetricEvent(CountMetricEventInstance countMetricEvent);

        /// <summary>
        /// Processes a logged amount metric event.
        /// </summary>
        /// <param name="amountMetricEvent">The amount metric event to process.</param>
        /// <remarks>Implementations of this method define how an amount metric event should be processed after it has been retrieved from the internal buffer queue.  The event could for example be written to a database, or to the console.</remarks>
        protected abstract void ProcessAmountMetricEvent(AmountMetricEventInstance amountMetricEvent);

        /// <summary>
        /// Processes a logged status metric event.
        /// </summary>
        /// <param name="statusMetricEvent">The status metric event to process.</param>
        /// <remarks>Implementations of this method define how a status metric event should be processed after it has been retrieved from the internal buffer queue.  The event could for example be written to a database, or to the console.</remarks>
        protected abstract void ProcessStatusMetricEvent(StatusMetricEventInstance statusMetricEvent);

        /// <summary>
        /// Processes a logged interval metric event.
        /// </summary>
        /// <param name="intervalMetricEvent">The interval metric event to process.</param>
        /// <param name="duration">The duration of the interval metric event in milliseconds.</param>
        /// <remarks>Implementations of this method define how an interval metric event should be processed after it has been retrieved from the internal buffer queue.  The event could for example be written to a database, or to the console.</remarks>
        protected abstract void ProcessIntervalMetricEvent(IntervalMetricEventInstance intervalMetricEvent, long duration);

        #endregion

        #region Private/Protected Methods

        /// <summary>
        /// Dequeues and processes metric events stored in the internal buffer.
        /// </summary>
        protected virtual void DequeueAndProcessMetricEvents()
        {
            DequeueAndProcessCountMetricEvents();
            DequeueAndProcessAmountMetricEvents();
            DequeueAndProcessStatusMetricEvents();
            DequeueAndProcessIntervalMetricEvents();
        }

        /// <summary>
        /// Returns 
        /// </summary>
        /// <returns></returns>
        protected System.DateTime GetStopWatchUtcNow()
        {
            return startTime.AddTicks(stopWatch.ElapsedTicks);
        }

        /// <summary>
        /// Dequeues count metric events stored in the internal buffer and calls abstract method ProcessCountMetricEvent() to process them.
        /// </summary>
        private void DequeueAndProcessCountMetricEvents()
        {
            Queue<CountMetricEventInstance> tempQueue;

            // Lock the count metric queue and move all items to the temporary queue
            lock (countMetricEventQueueLock)
            {
                tempQueue = new Queue<CountMetricEventInstance>(countMetricEventQueue);
                countMetricEventQueue.Clear();
                bufferProcessingStrategy.NotifyCountMetricEventBufferCleared();
            }

            // Process all items in the temporary queue
            while (tempQueue.Count > 0)
            {
                CountMetricEventInstance currentCountMetricEvent = tempQueue.Dequeue();
                ProcessCountMetricEvent(currentCountMetricEvent);
            }
        }

        /// <summary>
        /// Dequeues amount metric events stored in the internal buffer and calls abstract method ProcessAmountMetricEvent() to process them.
        /// </summary>
        private void DequeueAndProcessAmountMetricEvents()
        {
            Queue<AmountMetricEventInstance> tempQueue;

            // Lock the amount metric queue and move all items to the temporary queue
            lock (amountMetricEventQueueLock)
            {
                tempQueue = new Queue<AmountMetricEventInstance>(amountMetricEventQueue);
                amountMetricEventQueue.Clear();
                bufferProcessingStrategy.NotifyAmountMetricEventBufferCleared();
            }

            // Process all items in the temporary queue
            while (tempQueue.Count > 0)
            {
                ProcessAmountMetricEvent(tempQueue.Dequeue());
            }
        }

        /// <summary>
        /// Dequeues status metric events stored in the internal buffer and calls abstract method ProcessStatusMetricEvent() to process them.
        /// </summary>
        private void DequeueAndProcessStatusMetricEvents()
        {
            Queue<StatusMetricEventInstance> tempQueue;

            // Lock the status metric queue and move all items to the temporary queue
            lock (statusMetricEventQueueLock)
            {
                tempQueue = new Queue<StatusMetricEventInstance>(statusMetricEventQueue);
                statusMetricEventQueue.Clear();
                bufferProcessingStrategy.NotifyStatusMetricEventBufferCleared();
            }

            // Process all items in the temporary queue
            while (tempQueue.Count > 0)
            {
                ProcessStatusMetricEvent(tempQueue.Dequeue());
            }
        }

        /// <summary>
        /// Dequeues interval metric events stored in the internal buffer and calls abstract method ProcessIntervalMetricEvent() to process them.
        /// </summary>
        private void DequeueAndProcessIntervalMetricEvents()
        {
            Queue<IntervalMetricEventInstance> tempQueue;

            // Lock the interval metric queue and move all items to the temporary queue
            lock (intervalMetricEventQueueLock)
            {
                tempQueue = new Queue<IntervalMetricEventInstance>(intervalMetricEventQueue);
                intervalMetricEventQueue.Clear();
                bufferProcessingStrategy.NotifyIntervalMetricEventBufferCleared();
            }

            // Process all items in the temporary queue
            while (tempQueue.Count > 0)
            {
                IntervalMetricEventInstance currentIntervalMetricEvent = tempQueue.Dequeue();

                switch (currentIntervalMetricEvent.TimePoint)
                {
                    // If the current interval metric represents the start of the interval, put it in the dictionary object 
                    case IntervalMetricEventTimePoint.Start:
                        if (startIntervalMetricEventStore.ContainsKey(currentIntervalMetricEvent.MetricType) == true)
                        {
                            // If a start interval event of this type was already received and checking is enabled, throw an exception
                            if (intervalMetricChecking == true)
                            {
                                exceptionHandler.Handle(new InvalidOperationException("Received duplicate begin '" + currentIntervalMetricEvent.Metric.Name + "' metrics."));
                            }
                            // If checking is not enabled, replace the currently stored begin interval event with the new one
                            else
                            {
                                startIntervalMetricEventStore.Remove(currentIntervalMetricEvent.MetricType);
                                startIntervalMetricEventStore.Add(currentIntervalMetricEvent.MetricType, currentIntervalMetricEvent);
                            }
                        }
                        else
                        {
                            startIntervalMetricEventStore.Add(currentIntervalMetricEvent.MetricType, currentIntervalMetricEvent);
                        }
                        break;

                    // If the current interval metric represents the end of the interval, call the method to process it
                    case IntervalMetricEventTimePoint.End:
                        if (startIntervalMetricEventStore.ContainsKey(currentIntervalMetricEvent.MetricType) == true)
                        {
                            TimeSpan intervalDuration = currentIntervalMetricEvent.EventTime.Subtract(startIntervalMetricEventStore[currentIntervalMetricEvent.MetricType].EventTime);
                            double intervalDurationMillisecondsDouble = intervalDuration.TotalMilliseconds;
                            // If the duration is less then 0 set back to 0, as the start time could be after the end time in the case the metric event occurred across a system time update
                            if (intervalDurationMillisecondsDouble < 0)
                            {
                                intervalDurationMillisecondsDouble = 0;
                            }
                            // Convert double to long
                            //   There should not be a risk of overflow here, as the number of milliseconds between DateTime.MinValue and DateTime.MaxValue is 315537897600000, which is a valid long value
                            long intervalDurationMillisecondsLong = Convert.ToInt64(intervalDurationMillisecondsDouble);

                            ProcessIntervalMetricEvent(startIntervalMetricEventStore[currentIntervalMetricEvent.MetricType], intervalDurationMillisecondsLong);

                            startIntervalMetricEventStore.Remove(currentIntervalMetricEvent.MetricType);
                        }
                        else
                        {
                            // If no corresponding start interval event of this type exists and checking is enabled, throw an exception
                            if (intervalMetricChecking == true)
                            {
                                exceptionHandler.Handle(new InvalidOperationException("Received end '" + currentIntervalMetricEvent.Metric.Name + "' with no corresponding start interval metric."));
                            }
                            // If checking is not enabled discard the interval event
                        }
                        break;

                    // If the current interval metric represents the cancelling of the interval, remove it from the dictionary object 
                    case IntervalMetricEventTimePoint.Cancel:
                        if (startIntervalMetricEventStore.ContainsKey(currentIntervalMetricEvent.MetricType) == true)
                        {
                            startIntervalMetricEventStore.Remove(currentIntervalMetricEvent.MetricType);
                        }
                        else
                        {
                            // If no corresponding start interval event of this type exists and checking is enabled, throw an exception
                            if (intervalMetricChecking == true)
                            {
                                exceptionHandler.Handle(new InvalidOperationException("Received cancel '" + currentIntervalMetricEvent.Metric.Name + "' with no corresponding start interval metric."));
                            }
                            // If checking is not enabled discard the interval event
                        }
                        break;
                }
            }
        }

        // TODO: Ideally the below method should be called at the top of all public/protected methods however, concerned about performance impact as these methods are called frequently
        //         May implement this at a later stage.

        /// <summary>
        /// Throws an ObjectDisposedException if Dispose() has been called on the object.
        /// </summary>
        private void ThrowExceptionIfDisposed()
        {
            if (disposed == true)
            {
                throw new ObjectDisposedException(this.GetType().Name);
            }
        }

        #endregion

        #region Finalize / Dispose Methods

        /// <summary>
        /// Releases the unmanaged resources used by the MetricLoggerBuffer.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #pragma warning disable 1591
        ~MetricLoggerBuffer()
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
                    bufferProcessingStrategy.BufferProcessed -= bufferProcessedEventHandler;
                }
                // Free your own state (unmanaged objects).

                // Set large fields to null.

                disposed = true;
            }
        }

        #endregion
    }
}
