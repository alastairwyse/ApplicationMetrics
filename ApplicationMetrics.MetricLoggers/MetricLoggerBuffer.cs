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
using System.Threading;
using StandardAbstraction;

namespace ApplicationMetrics.MetricLoggers
{
    /// <summary>
    /// Base class which acts as a buffer for implementations of interface IMetricLogger.  Stores logged metrics events in queues, so as to minimise the time taken to call the logging methods.
    /// </summary>
    /// <remarks>Derived classes must implement methods which process the buffered metric events (e.g. ProcessIntervalMetricEvent()).  These methods are called from a worker thread after dequeueing the buffered metric events.</remarks>
    public abstract class MetricLoggerBuffer : IMetricLogger, IDisposable
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
        protected MetricLoggerBuffer(IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking, IDateTime dateTime, IStopwatch stopWatch)
            : this(bufferProcessingStrategy, intervalMetricChecking)
        {
            this.dateTime = dateTime;
            this.stopWatch = stopWatch;
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
        /// Processes logged count metric events.
        /// </summary>
        /// <param name="countMetricEvents">The count metric events to process.</param>
        /// <remarks>Implementations of this method define how the internal buffer of count metric events should be processed.  The events could for example be written to a database, or to the console.</remarks>
        protected abstract void ProcessCountMetricEvents(Queue<CountMetricEventInstance> countMetricEvents);

        /// <summary>
        /// Processes logged amount metric events.
        /// </summary>
        /// <param name="amountMetricEvents">The amount metric events to process.</param>
        /// <remarks>Implementations of this method define how the internal buffer of amount metric events should be processed.  The events could for example be written to a database, or to the console.</remarks>
        protected abstract void ProcessAmountMetricEvents(Queue<AmountMetricEventInstance> amountMetricEvents);

        /// <summary>
        /// Processes logged status metric events.
        /// </summary>
        /// <param name="statusMetricEvents">The status metric events to process.</param>
        /// <remarks>Implementations of this method define how the internal buffer of status metric event should be processed.  The events could for example be written to a database, or to the console.</remarks>
        protected abstract void ProcessStatusMetricEvents(Queue<StatusMetricEventInstance> statusMetricEvents);

        /// <summary>
        /// Processes logged interval metric events.
        /// </summary>
        /// <param name="intervalMetricEventsAndDurations">The interval metric events and corresponding durations of the events (in milliseconds) to process.</param>
        /// <remarks>Implementations of this method define how buffered interval metric events should be processed.  The events could for example be written to a database, or to the console.</remarks>
        protected abstract void ProcessIntervalMetricEvents(Queue<Tuple<IntervalMetricEventInstance, Int64>> intervalMetricEventsAndDurations);

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
            var tempQueue = new Queue<CountMetricEventInstance>();

            // Lock the count metric queue and move all items to the temporary queue
            lock (countMetricEventQueueLock)
            {
                Interlocked.Exchange(ref tempQueue, countMetricEventQueue);
                countMetricEventQueue = new Queue<CountMetricEventInstance>();
                bufferProcessingStrategy.NotifyCountMetricEventBufferCleared();
            }

            ProcessCountMetricEvents(tempQueue);
        }

        /// <summary>
        /// Dequeues amount metric events stored in the internal buffer and calls abstract method ProcessAmountMetricEvent() to process them.
        /// </summary>
        private void DequeueAndProcessAmountMetricEvents()
        {
            var tempQueue = new Queue<AmountMetricEventInstance>();

            // Lock the amount metric queue and move all items to the temporary queue
            lock (amountMetricEventQueueLock)
            {
                Interlocked.Exchange(ref tempQueue, amountMetricEventQueue);
                amountMetricEventQueue = new Queue<AmountMetricEventInstance>();
                bufferProcessingStrategy.NotifyAmountMetricEventBufferCleared();
            }

            ProcessAmountMetricEvents(tempQueue);
        }

        /// <summary>
        /// Dequeues status metric events stored in the internal buffer and calls abstract method ProcessStatusMetricEvent() to process them.
        /// </summary>
        private void DequeueAndProcessStatusMetricEvents()
        {
            var tempQueue = new Queue<StatusMetricEventInstance>();

            // Lock the status metric queue and move all items to the temporary queue
            lock (statusMetricEventQueueLock)
            {
                Interlocked.Exchange(ref tempQueue, statusMetricEventQueue);
                statusMetricEventQueue = new Queue<StatusMetricEventInstance>();
                bufferProcessingStrategy.NotifyStatusMetricEventBufferCleared();
            }

            ProcessStatusMetricEvents(tempQueue);
        }

        /// <summary>
        /// Dequeues interval metric events stored in the internal buffer and calls abstract method ProcessIntervalMetricEvent() to process them.
        /// </summary>
        private void DequeueAndProcessIntervalMetricEvents()
        {
            var tempQueue = new Queue<IntervalMetricEventInstance>();
            var intervalMetricsAndDurations = new Queue<Tuple<IntervalMetricEventInstance, Int64>>();

            // Lock the interval metric queue and move all items to the temporary queue
            lock (intervalMetricEventQueueLock)
            {
                Interlocked.Exchange(ref tempQueue, intervalMetricEventQueue);
                intervalMetricEventQueue = new Queue<IntervalMetricEventInstance>();
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
                                throw new InvalidOperationException("Received duplicate begin '" + currentIntervalMetricEvent.Metric.Name + "' metrics.");
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
                            // Convert double to an Int64
                            //   There should not be a risk of overflow here, as the number of milliseconds between DateTime.MinValue and DateTime.MaxValue is 315537897600000, which is a valid Int64 value
                            Int64 intervalDurationMillisecondsInt64 = Convert.ToInt64(intervalDurationMillisecondsDouble);
                            intervalMetricsAndDurations.Enqueue(new Tuple<IntervalMetricEventInstance, Int64>(startIntervalMetricEventStore[currentIntervalMetricEvent.MetricType], intervalDurationMillisecondsInt64));
                            startIntervalMetricEventStore.Remove(currentIntervalMetricEvent.MetricType);
                        }
                        else
                        {
                            // If no corresponding start interval event of this type exists and checking is enabled, throw an exception
                            if (intervalMetricChecking == true)
                            {
                                throw new InvalidOperationException("Received end '" + currentIntervalMetricEvent.Metric.Name + "' with no corresponding start interval metric.");
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
                                throw new InvalidOperationException("Received cancel '" + currentIntervalMetricEvent.Metric.Name + "' with no corresponding start interval metric.");
                            }
                            // If checking is not enabled discard the interval event
                        }
                        break;
                }
            }

            ProcessIntervalMetricEvents(intervalMetricsAndDurations);
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

        #region Nested Classes

        /// <summary>
        /// Represents the time point of an instance of an interval metric event.
        /// </summary>
        protected enum IntervalMetricEventTimePoint
        {
            /// <summary>The start of the interval metric event.</summary>
            Start,
            /// <summary>The completion of the interval metric event.</summary>
            End,
            /// <summary>The cancellation of a previously started interval metric event.</summary>
            Cancel
        }

        /// <summary>
        /// Base container class which stores information about the occurrence of a metric event.
        /// </summary>
        /// <typeparam name="T">The type of metric the event information should be stored for.</typeparam>
        protected abstract class MetricEventInstance<T> where T : MetricBase
        {
            /// <summary>The metric that occurred.</summary>
            protected T metric;
            /// <summary>The date and time the event occurred, expressed as UTC.</summary>
            protected System.DateTime eventTime;

            /// <summary>
            /// The metric that occurred.
            /// </summary>
            public T Metric
            {
                get
                {
                    return metric;
                }
            }

            /// <summary>
            /// Returns the type of the metric that occurred.
            /// </summary>
            public Type MetricType
            {
                get
                {
                    return metric.GetType();
                }
            }

            /// <summary>
            /// The date and time the event occurred, expressed as UTC.
            /// </summary>
            public System.DateTime EventTime
            {
                get
                {
                    return eventTime;
                }
            }
        }

        /// <summary>
        /// Container class which stores information about the occurrence of an amount metric event.
        /// </summary>
        protected class AmountMetricEventInstance : MetricEventInstance<AmountMetric>
        {
            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBuffer+AmountMetricEventInstance class.
            /// </summary>
            /// <param name="amountMetric">The metric which occurred.</param>
            /// <param name="eventTime">The date and time the metric event occurred, expressed as UTC.</param>
            public AmountMetricEventInstance(AmountMetric amountMetric, System.DateTime eventTime)
            {
                base.metric = amountMetric;
                base.eventTime = eventTime;
            }
        }

        /// <summary>
        /// Container class which stores information about the occurrence of a count metric event.
        /// </summary>
        protected class CountMetricEventInstance : MetricEventInstance<CountMetric>
        {
            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBuffer+CountMetricEventInstance class.
            /// </summary>
            /// <param name="countMetric">The metric which occurred.</param>
            /// <param name="eventTime">The date and time the metric event occurred, expressed as UTC.</param>
            public CountMetricEventInstance(CountMetric countMetric, System.DateTime eventTime)
            {
                base.metric = countMetric;
                base.eventTime = eventTime;
            }
        }

        /// <summary>
        /// Container class which stores information about the occurrence of a status metric event.
        /// </summary>
        protected class StatusMetricEventInstance : MetricEventInstance<StatusMetric>
        {
            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBuffer+StatusMetricEventInstance class.
            /// </summary>
            /// <param name="statusMetric">The metric which occurred.</param>
            /// <param name="eventTime">The date and time the metric event occurred, expressed as UTC.</param>
            public StatusMetricEventInstance(StatusMetric statusMetric, System.DateTime eventTime)
            {
                base.metric = statusMetric;
                base.eventTime = eventTime;
            }
        }

        /// <summary>
        /// Container class which stores information about the occurrence of an interval metric event.
        /// </summary>
        protected class IntervalMetricEventInstance : MetricEventInstance<IntervalMetric>
        {
            private IntervalMetricEventTimePoint timePoint;

            /// <summary>
            /// Whether the event represents the start or the end of the interval metric.
            /// </summary>
            public IntervalMetricEventTimePoint TimePoint
            {
                get
                {
                    return timePoint;
                }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBuffer+IntervalMetricEventInstance class.
            /// </summary>
            /// <param name="intervalMetric">The metric which occurred.</param>
            /// <param name="timePoint">Whether the event represents the start or the end of the interval metric.</param>
            /// <param name="eventTime">The date and time the metric event started, expressed as UTC.</param>
            public IntervalMetricEventInstance(IntervalMetric intervalMetric, IntervalMetricEventTimePoint timePoint, System.DateTime eventTime)
            {
                base.metric = intervalMetric;
                this.timePoint = timePoint;
                base.eventTime = eventTime;
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
