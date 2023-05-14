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
    /// <remarks>
    /// <para>Derived classes must implement methods which process the buffered metric events (e.g. ProcessIntervalMetricEvent()).  These methods are called from a worker thread after dequeueing the buffered metric events.</para>
    /// <para>Since version 5.0.0, the class supports an 'interleaved' mode for interval metrics.  This allows multiple interval metrics of the same type to be started concurrently (e.g. to support scenarios where client code is running on multiple concurrent threads).  The <see cref="IMetricLogger.Begin(IntervalMetric)"/>, <see cref="IMetricLogger.End(Guid, IntervalMetric)"/>, and <see cref="IMetricLogger.CancelBegin(Guid, IntervalMetric)"/> methods support an optional <see cref="Guid"/> return value and parameter which is used to associate/link calls to those methods.  For backwards compatability the non-<see cref="Guid"/> versions of these methods are maintained.  The mode (interleaved or non-interleaved) is chosen on the first call to either the <see cref="IMetricLogger.End(Guid, IntervalMetric)"/> or <see cref="IMetricLogger.CancelBegin(Guid, IntervalMetric)"/> methods, selecting interleaved mode if the <see cref="Guid"/> overload versions of the methods are called, and non-interleaved if the non-<see cref="Guid"/> overload versions are called.  Once the mode is set, calling method overloads corresponding to the other mode will throw an exception, so client code must consistently call either the <see cref="Guid"/> or non-<see cref="Guid"/> overloads of these methods.  Non-interleaved mode may be deprecated in future versions, so it is recommended to migrate client code to support interleaved mode.</para>
    /// </remarks>
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
        protected Queue<UniqueIntervalMetricEventInstance> intervalMetricEventQueue;

        // Lock objects for queues
        /// <summary>Lock object which should be set before dequeuing from queue countMetricEventQueue.</summary>
        protected readonly object countMetricEventQueueLock;
        /// <summary>Lock object which should be set before dequeuing from queue amountMetricEventQueue.</summary>
        protected readonly object amountMetricEventQueueLock;
        /// <summary>Lock object which should be set before dequeuing from queue statusMetricEventQueue.</summary>
        protected readonly object statusMetricEventQueueLock;
        /// <summary>Lock object which should be set before dequeuing from queue intervalMetricEventQueue.</summary>
        protected readonly object intervalMetricEventQueueLock;

        /// <summary>Object which implements a processing strategy for the buffers (queues).</summary>
        protected readonly IBufferProcessingStrategy bufferProcessingStrategy;
        /// <summary>The base time unit to use to log interval metrics.</summary>
        protected readonly IntervalMetricBaseTimeUnit intervalMetricBaseTimeUnit;
        /// <summary>The delegate to handle when a BufferProcessed event is raised.</summary>
        protected readonly EventHandler bufferProcessedEventHandler;
        /// <summary>Object which provides the current date and time.</summary>
        protected readonly IDateTime dateTime;
        /// <summary>Used to measure elapsed time since starting the buffer processor.</summary>
        protected readonly IStopwatch stopWatch;
        /// <summary>Object which provides Guids.</summary>
        protected readonly IGuidProvider guidProvider;
        /// <summary>Whether to support interleaving when processing interval metrics.  Set to null when the mode has not yet been determined (i.e. before the <see cref="IMetricLogger.End(Guid, IntervalMetric)"/> or <see cref="IMetricLogger.CancelBegin(Guid, IntervalMetric)"/> methods have been called).</summary>
        protected Nullable<Boolean> interleavedIntervalMetricsMode;
        /// <summary>The timestamp at which the buffer processor was started.</summary>
        protected System.DateTime startTime;
        /// <summary>Indicates whether the object has been disposed.</summary>
        protected bool disposed;

        // Dictionary object to temporarily store the start instance of any received interval metrics (when processing in non-interleaved mode)
        private Dictionary<Type, IntervalMetricEventInstance> startIntervalMetricEventStore;
        // Dictionary object to temporarily store the start instance of any received interval metrics (when processing in interleaved mode)
        private Dictionary<Guid, IntervalMetricEventInstance> startIntervalMetricUniqueEventStore;
        // Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed(e.g.End() method called before Begin()).
        private bool intervalMetricChecking;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBuffer class.
        /// </summary>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricBaseTimeUnit">The base time unit to use to log interval metrics.</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  Note that this parameter only has an effect when running in 'non-interleaved' mode.</param>
        /// <remarks>The class uses a <see cref="Stopwatch"/> to calculate and log interval metrics.  Since the smallest unit of time supported by Stopwatch is a tick (100 nanoseconds), the smallest level of granularity supported when constructor parameter 'intervalMetricBaseTimeUnit' is set to <see cref="IntervalMetricBaseTimeUnit.Nanosecond"/> is 100 nanoseconds.</remarks>
        protected MetricLoggerBuffer(IBufferProcessingStrategy bufferProcessingStrategy, IntervalMetricBaseTimeUnit intervalMetricBaseTimeUnit, bool intervalMetricChecking)
        {
            countMetricEventQueue = new Queue<CountMetricEventInstance>();
            amountMetricEventQueue = new Queue<AmountMetricEventInstance>();
            statusMetricEventQueue = new Queue<StatusMetricEventInstance>();
            intervalMetricEventQueue = new Queue<UniqueIntervalMetricEventInstance>();
            countMetricEventQueueLock = new object();
            amountMetricEventQueueLock = new object();
            statusMetricEventQueueLock = new object();
            intervalMetricEventQueueLock = new object();

            this.bufferProcessingStrategy = bufferProcessingStrategy;
            this.intervalMetricBaseTimeUnit = intervalMetricBaseTimeUnit;
            bufferProcessedEventHandler = delegate(object sender, EventArgs e) { DequeueAndProcessMetricEvents(); };
            this.bufferProcessingStrategy.BufferProcessed += bufferProcessedEventHandler;
            dateTime = new StandardAbstraction.DateTime();
            stopWatch = new Stopwatch();
            guidProvider = new DefaultGuidProvider();

            interleavedIntervalMetricsMode = null;
            startIntervalMetricEventStore = new Dictionary<Type, IntervalMetricEventInstance>();
            startIntervalMetricUniqueEventStore = new Dictionary<Guid, IntervalMetricEventInstance>();
            this.intervalMetricChecking = intervalMetricChecking;
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBuffer class.  Note this is an additional constructor to facilitate unit tests, and should not be used to instantiate the class under normal conditions.
        /// </summary>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricBaseTimeUnit">The base time unit to use to log interval metrics.</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  Note that this parameter only has an effect when running in 'non-interleaved' mode.</param>
        /// <param name="dateTime">A test (mock) <see cref="IDateTime"/> object.</param>
        /// <param name="stopWatch">A test (mock) <see cref="IStopwatch"/> object.</param>
        /// <param name="guidProvider">A test (mock) <see cref="IGuidProvider"/> object.</param>
        /// <remarks>This constructor is included to facilitate unit testing.</remarks>
        protected MetricLoggerBuffer(IBufferProcessingStrategy bufferProcessingStrategy, IntervalMetricBaseTimeUnit intervalMetricBaseTimeUnit, bool intervalMetricChecking, IDateTime dateTime, IStopwatch stopWatch, IGuidProvider guidProvider)
            : this(bufferProcessingStrategy, intervalMetricBaseTimeUnit, intervalMetricChecking)
        {
            this.dateTime = dateTime;
            this.stopWatch = stopWatch;
            this.guidProvider = guidProvider;
        }

        /// <summary>
        /// Starts the buffer processing (e.g. if the implementation of the buffer processing strategy uses a worker thread, this method starts the worker thread).
        /// </summary>
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
        public virtual void Stop()
        {
            bufferProcessingStrategy.Stop();
            stopWatch.Stop();
        }

        /// <inheritdoc/>
        public void Increment(CountMetric countMetric)
        {
            lock (countMetricEventQueueLock)
            {
                countMetricEventQueue.Enqueue(new CountMetricEventInstance(countMetric, GetStopWatchUtcNow()));
                bufferProcessingStrategy.NotifyCountMetricEventBuffered();
            }
        }

        /// <inheritdoc/>
        public void Add(AmountMetric amountMetric, long amount)
        {
            lock (amountMetricEventQueueLock)
            {
                amountMetricEventQueue.Enqueue(new AmountMetricEventInstance(amountMetric, amount, GetStopWatchUtcNow()));
                bufferProcessingStrategy.NotifyAmountMetricEventBuffered();
            }
        }

        /// <inheritdoc/>
        public void Set(StatusMetric statusMetric, long value)
        {
            lock (statusMetricEventQueueLock)
            {
                statusMetricEventQueue.Enqueue(new StatusMetricEventInstance(statusMetric, value, GetStopWatchUtcNow()));
                bufferProcessingStrategy.NotifyStatusMetricEventBuffered();
            }
        }

        /// <inheritdoc/>
        public Guid Begin(IntervalMetric intervalMetric)
        {
            lock (intervalMetricEventQueueLock)
            {
                Guid beginId = guidProvider.NewGuid();
                intervalMetricEventQueue.Enqueue(new UniqueIntervalMetricEventInstance(beginId, intervalMetric, IntervalMetricEventTimePoint.Start, GetStopWatchUtcNow()));

                return beginId;
            }
        }

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">This method overload cannot be called when the metric logger is running in interleaved mode.</exception>
        public void End(IntervalMetric intervalMetric)
        {
            if (interleavedIntervalMetricsMode.HasValue == false)
            {
                interleavedIntervalMetricsMode = false;
            }
            else
            {
                if (interleavedIntervalMetricsMode == true)
                    throw new InvalidOperationException($"The overload of the {nameof(IMetricLogger.End)}() method without a {nameof(Guid)} parameter cannot be called when the metric logger is running in interleaved mode.");
            }

            lock (intervalMetricEventQueueLock)
            {
                intervalMetricEventQueue.Enqueue(new UniqueIntervalMetricEventInstance(Guid.Empty, intervalMetric, IntervalMetricEventTimePoint.End, GetStopWatchUtcNow()));
                bufferProcessingStrategy.NotifyIntervalMetricEventBuffered();
            }
        }

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">This method overload cannot be called when the metric logger is running in non-interleaved mode.</exception>
        public void End(Guid beginId, IntervalMetric intervalMetric)
        {
            if (interleavedIntervalMetricsMode.HasValue == false)
            {
                interleavedIntervalMetricsMode = true;
            }
            else
            {
                if (interleavedIntervalMetricsMode == false)
                    throw new InvalidOperationException($"The overload of the {nameof(IMetricLogger.End)}() method with a {nameof(Guid)} parameter cannot be called when the metric logger is running in non-interleaved mode.");
            }

            lock (intervalMetricEventQueueLock)
            {
                intervalMetricEventQueue.Enqueue(new UniqueIntervalMetricEventInstance(beginId, intervalMetric, IntervalMetricEventTimePoint.End, GetStopWatchUtcNow()));
                bufferProcessingStrategy.NotifyIntervalMetricEventBuffered();
            }
        }

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">This method overload cannot be called when the metric logger is running in interleaved mode.</exception>
        public void CancelBegin(IntervalMetric intervalMetric)
        {
            if (interleavedIntervalMetricsMode.HasValue == false)
            {
                interleavedIntervalMetricsMode = false;
            }
            else
            {
                if (interleavedIntervalMetricsMode == true)
                    throw new InvalidOperationException($"The overload of the {nameof(IMetricLogger.CancelBegin)}() method without a {nameof(Guid)} parameter cannot be called when the metric logger is running in interleaved mode.");
            }

            lock (intervalMetricEventQueueLock)
            {
                intervalMetricEventQueue.Enqueue(new UniqueIntervalMetricEventInstance(Guid.Empty, intervalMetric, IntervalMetricEventTimePoint.Cancel, GetStopWatchUtcNow()));
            }
        }

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">This method overload cannot be called when the metric logger is running in non-interleaved mode.</exception>
        public void CancelBegin(Guid beginId, IntervalMetric intervalMetric)
        {
            if (interleavedIntervalMetricsMode.HasValue == false)
            {
                interleavedIntervalMetricsMode = true;
            }
            else
            {
                if (interleavedIntervalMetricsMode == false)
                    throw new InvalidOperationException($"The overload of the {nameof(IMetricLogger.CancelBegin)}() method with a {nameof(Guid)} parameter cannot be called when the metric logger is running in non-interleaved mode.");
            }

            lock (intervalMetricEventQueueLock)
            {
                intervalMetricEventQueue.Enqueue(new UniqueIntervalMetricEventInstance(beginId, intervalMetric, IntervalMetricEventTimePoint.Cancel, GetStopWatchUtcNow()));
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
            var tempQueue = new Queue<UniqueIntervalMetricEventInstance>();
            var intervalMetricsAndDurations = new Queue<Tuple<IntervalMetricEventInstance, Int64>>();

            // Lock the interval metric queue and move all items to the temporary queue
            lock (intervalMetricEventQueueLock)
            {
                Interlocked.Exchange(ref tempQueue, intervalMetricEventQueue);
                intervalMetricEventQueue = new Queue<UniqueIntervalMetricEventInstance>();
                bufferProcessingStrategy.NotifyIntervalMetricEventBufferCleared();
            }

            // Process all items in the temporary queue
            while (tempQueue.Count > 0)
            {
                UniqueIntervalMetricEventInstance currentIntervalMetricEvent = tempQueue.Dequeue();

                switch (currentIntervalMetricEvent.TimePoint)
                {
                    // If the current interval metric represents the start of the interval, put it in the dictionary object 
                    case IntervalMetricEventTimePoint.Start:
                        ProcessStartIntervalMetricEvent(currentIntervalMetricEvent);
                        break;

                    // If the current interval metric represents the end of the interval, call the method to process it
                    case IntervalMetricEventTimePoint.End:
                        ProcessEndIntervalMetricEvent(currentIntervalMetricEvent, intervalMetricsAndDurations);
                        break;

                    // If the current interval metric represents the cancelling of the interval, remove it from the dictionary object 
                    case IntervalMetricEventTimePoint.Cancel:
                        ProcessCancelIntervalMetricEvent(currentIntervalMetricEvent);
                        break;
                }
            }

            ProcessIntervalMetricEvents(intervalMetricsAndDurations);
        }

        /// <summary>
        /// Processes a <see cref="UniqueIntervalMetricEventInstance"/> with a <see cref="IntervalMetricEventTimePoint.Start">'Start'</see> <see cref="IntervalMetricEventInstance.TimePoint">TimePoint</see> as part of the call the to <see cref="MetricLoggerBuffer.DequeueAndProcessIntervalMetricEvents">DequeueAndProcessIntervalMetricEvents</see>() method.
        /// </summary>
        /// <param name="intervalMetricEventInstance">The <see cref="UniqueIntervalMetricEventInstance"/> to process.</param>
        private void ProcessStartIntervalMetricEvent(UniqueIntervalMetricEventInstance intervalMetricEventInstance)
        {
            // Need to handle the case that 'interleavedIntervalMetricsMode' has not yet been set
            if (interleavedIntervalMetricsMode.HasValue == false || interleavedIntervalMetricsMode == false)
            {
                if (startIntervalMetricEventStore.ContainsKey(intervalMetricEventInstance.MetricType) == true)
                {
                    // If a start interval event of this type was already received and checking is enabled, throw an exception
                    if (intervalMetricChecking == true)
                    {
                        throw new InvalidOperationException($"Received duplicate begin '{intervalMetricEventInstance.Metric.Name}' metrics.");
                    }
                    // If checking is not enabled, replace the currently stored begin interval event with the new one
                    else
                    {
                        startIntervalMetricEventStore.Remove(intervalMetricEventInstance.MetricType);
                        startIntervalMetricEventStore.Add(intervalMetricEventInstance.MetricType, intervalMetricEventInstance);
                    }
                }
                else
                {
                    startIntervalMetricEventStore.Add(intervalMetricEventInstance.MetricType, intervalMetricEventInstance);
                }
            }
            if (interleavedIntervalMetricsMode.HasValue == false || interleavedIntervalMetricsMode == true)
            {
                startIntervalMetricUniqueEventStore.Add(intervalMetricEventInstance.BeginId, intervalMetricEventInstance);
            }
        }

        /// <summary>
        /// Processes an <see cref="UniqueIntervalMetricEventInstance"/> with a <see cref="IntervalMetricEventTimePoint.End">'End'</see> <see cref="IntervalMetricEventInstance.TimePoint">TimePoint</see> as part of the call the to <see cref="MetricLoggerBuffer.DequeueAndProcessIntervalMetricEvents">DequeueAndProcessIntervalMetricEvents</see>() method.
        /// </summary>
        /// <param name="intervalMetricEventInstance">The <see cref="UniqueIntervalMetricEventInstance"/> to process.</param>
        /// <param name="intervalMetricsAndDurations">A queue containing the processed <see cref="IntervalMetricEventInstance">IntervalMetricEventInstances</see> and their durations.</param>
        private void ProcessEndIntervalMetricEvent(UniqueIntervalMetricEventInstance intervalMetricEventInstance, Queue<Tuple<IntervalMetricEventInstance, Int64>> intervalMetricsAndDurations)
        {
            if (interleavedIntervalMetricsMode == false)
            {
                if (startIntervalMetricEventStore.ContainsKey(intervalMetricEventInstance.MetricType) == true)
                {
                    TimeSpan intervalDuration = intervalMetricEventInstance.EventTime.Subtract(startIntervalMetricEventStore[intervalMetricEventInstance.MetricType].EventTime);
                    double intervalDurationTicks = intervalDuration.Ticks;
                    if (intervalDurationTicks < 0)
                    {
                        intervalDurationTicks = 0;
                    }
                    Int64 intervalDurationBaseTimeUnit;
                    if (intervalMetricBaseTimeUnit == IntervalMetricBaseTimeUnit.Millisecond)
                    {
                        intervalDurationBaseTimeUnit = Convert.ToInt64(intervalDurationTicks) / 10000;
                    }
                    else
                    {
                        // Below will only overflow if duration  is over 290 years, so should be safe
                        intervalDurationBaseTimeUnit = Convert.ToInt64(intervalDurationTicks * 100);
                    }
                    intervalMetricsAndDurations.Enqueue(new Tuple<IntervalMetricEventInstance, Int64>(startIntervalMetricEventStore[intervalMetricEventInstance.MetricType], intervalDurationBaseTimeUnit));
                    startIntervalMetricEventStore.Remove(intervalMetricEventInstance.MetricType);
                }
                else
                {
                    // If no corresponding start interval event of this type exists and checking is enabled, throw an exception
                    if (intervalMetricChecking == true)
                    {
                        throw new InvalidOperationException($"Received end '{intervalMetricEventInstance.Metric.Name}' with no corresponding start interval metric.");
                    }
                    // If checking is not enabled discard the interval event
                }
            }
            else
            {
                if (startIntervalMetricUniqueEventStore.ContainsKey(intervalMetricEventInstance.BeginId) == true)
                {
                    if (startIntervalMetricUniqueEventStore[intervalMetricEventInstance.BeginId].MetricType != intervalMetricEventInstance.MetricType)
                        throw new ArgumentException($"Metric started with BeginId '{intervalMetricEventInstance.BeginId}' was a '{startIntervalMetricUniqueEventStore[intervalMetricEventInstance.BeginId].Metric.Name}' metric, but {nameof(IMetricLogger.End)}() method was called with a '{intervalMetricEventInstance.Metric.Name}' metric.");

                    TimeSpan intervalDuration = intervalMetricEventInstance.EventTime.Subtract(startIntervalMetricUniqueEventStore[intervalMetricEventInstance.BeginId].EventTime);
                    double intervalDurationTicks = intervalDuration.Ticks;
                    if (intervalDurationTicks < 0)
                    {
                        intervalDurationTicks = 0;
                    }
                    Int64 intervalDurationBaseTimeUnit;
                    if (intervalMetricBaseTimeUnit == IntervalMetricBaseTimeUnit.Millisecond)
                    {
                        intervalDurationBaseTimeUnit = Convert.ToInt64(intervalDurationTicks) / 10000;
                    }
                    else
                    {
                        // Below will only overflow if duration  is over 290 years, so should be safe
                        intervalDurationBaseTimeUnit = Convert.ToInt64(intervalDurationTicks * 100);
                    }
                    intervalMetricsAndDurations.Enqueue(new Tuple<IntervalMetricEventInstance, Int64>(startIntervalMetricUniqueEventStore[intervalMetricEventInstance.BeginId], intervalDurationBaseTimeUnit));
                    startIntervalMetricUniqueEventStore.Remove(intervalMetricEventInstance.BeginId);
                }
                else
                {
                    throw new InvalidOperationException($"Received end '{intervalMetricEventInstance.Metric.Name}' with {nameof(UniqueIntervalMetricEventInstance.BeginId)} '{intervalMetricEventInstance.BeginId}' with no corresponding start interval metric.");
                }
            }
        }

        /// <summary>
        /// Processes an <see cref="UniqueIntervalMetricEventInstance"/> with a <see cref="IntervalMetricEventTimePoint.Cancel">'Cancel'</see> <see cref="IntervalMetricEventInstance.TimePoint">TimePoint</see> as part of the call the to <see cref="MetricLoggerBuffer.DequeueAndProcessIntervalMetricEvents">DequeueAndProcessIntervalMetricEvents</see>() method.
        /// </summary>
        /// <param name="intervalMetricEventInstance">The <see cref="UniqueIntervalMetricEventInstance"/> to process.</param>
        private void ProcessCancelIntervalMetricEvent(UniqueIntervalMetricEventInstance intervalMetricEventInstance)
        {
            if (interleavedIntervalMetricsMode == false)
            {
                if (startIntervalMetricEventStore.ContainsKey(intervalMetricEventInstance.MetricType) == true)
                {
                    startIntervalMetricEventStore.Remove(intervalMetricEventInstance.MetricType);
                }
                else
                {
                    // If no corresponding start interval event of this type exists and checking is enabled, throw an exception
                    if (intervalMetricChecking == true)
                    {
                        throw new InvalidOperationException("Received cancel '" + intervalMetricEventInstance.Metric.Name + "' with no corresponding start interval metric.");
                    }
                    // If checking is not enabled discard the interval event
                }
            }
            else
            {
                if (startIntervalMetricUniqueEventStore.ContainsKey(intervalMetricEventInstance.BeginId) == true)
                {
                    if (startIntervalMetricUniqueEventStore[intervalMetricEventInstance.BeginId].MetricType != intervalMetricEventInstance.MetricType)
                        throw new ArgumentException($"Metric started with BeginId '{intervalMetricEventInstance.BeginId}' was a '{startIntervalMetricUniqueEventStore[intervalMetricEventInstance.BeginId].Metric.Name}' metric, but {nameof(IMetricLogger.CancelBegin)}() method was called with a '{intervalMetricEventInstance.Metric.Name}' metric.");

                    startIntervalMetricUniqueEventStore.Remove(intervalMetricEventInstance.BeginId);
                }
                else
                {
                    throw new InvalidOperationException($"Received cancel '{intervalMetricEventInstance.Metric.Name}' with {nameof(UniqueIntervalMetricEventInstance.BeginId)} '{intervalMetricEventInstance.BeginId}' with no corresponding start interval metric.");
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

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBuffer+MetricEventInstance class.
            /// </summary>
            /// <param name="metric">The metric that occurred.</param>
            /// <param name="eventTime">The date and time the event occurred, expressed as UTC.</param>
            protected MetricEventInstance(T metric, System.DateTime eventTime)
            {
                this.metric = metric;
                this.eventTime = eventTime;
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
                : base(countMetric, eventTime)
            {
            }
        }

        /// <summary>
        /// Container class which stores information about the occurrence of an amount metric event.
        /// </summary>
        protected class AmountMetricEventInstance : MetricEventInstance<AmountMetric>
        {
            /// <summary>The amount associated with the instance of the amount metric.</summary>
            protected long amount;

            /// <summary>
            /// The amount associated with the instance of the amount metric.
            /// </summary>
            public long Amount
            {
                get { return amount; }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBuffer+AmountMetricEventInstance class.
            /// </summary>
            /// <param name="amountMetric">The metric which occurred.</param>
            /// <param name="amount">The amount associated with the instance of the amount metric.</param>
            /// <param name="eventTime">The date and time the metric event occurred, expressed as UTC.</param>
            public AmountMetricEventInstance(AmountMetric amountMetric, long amount, System.DateTime eventTime)
                : base(amountMetric, eventTime)
            {
                this.amount = amount;
            }
        }

        /// <summary>
        /// Container class which stores information about the occurrence of a status metric event.
        /// </summary>
        protected class StatusMetricEventInstance : MetricEventInstance<StatusMetric>
        {
            /// <summary>The value associated with the instance of the status metric.</summary>
            protected long value;

            /// <summary>
            /// The value associated with the instance of the status metric.
            /// </summary>
            public long Value
            {
                get { return value; }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBuffer+StatusMetricEventInstance class.
            /// </summary>
            /// <param name="statusMetric">The metric which occurred.</param>
            /// <param name="value">The value associated with the instance of the status metric.</param>
            /// <param name="eventTime">The date and time the metric event occurred, expressed as UTC.</param>
            public StatusMetricEventInstance(StatusMetric statusMetric, long value, System.DateTime eventTime)
                : base(statusMetric, eventTime)
            {
                this.value = value;
            }
        }

        /// <summary>
        /// Container class which stores information about the occurrence of an interval metric event.
        /// </summary>
        protected class IntervalMetricEventInstance : MetricEventInstance<IntervalMetric>
        {
            /// <summary>Whether the event represents the start or the end of the interval metric.</summary>
            protected IntervalMetricEventTimePoint timePoint;

            /// <summary>
            /// Whether the event represents the start or the end of the interval metric.
            /// </summary>
            public IntervalMetricEventTimePoint TimePoint
            {
                get { return timePoint; }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBuffer+IntervalMetricEventInstance class.
            /// </summary>
            /// <param name="intervalMetric">The metric which occurred.</param>
            /// <param name="timePoint">Whether the event represents the start or the end of the interval metric.</param>
            /// <param name="eventTime">The date and time the metric event started, expressed as UTC.</param>
            public IntervalMetricEventInstance(IntervalMetric intervalMetric, IntervalMetricEventTimePoint timePoint, System.DateTime eventTime)
                : base(intervalMetric, eventTime)
            {
                this.timePoint = timePoint;
            }
        }

        /// <summary>
        /// Container class which stores information about the occurrence of an interval metric event and includes a <see cref="Guid"/> property to allow the event to be uniquely identified within a collection of the same type of interval metric.
        /// </summary>
        protected class UniqueIntervalMetricEventInstance : IntervalMetricEventInstance
        {
            /// <summary>A unique id representing the starting of the interval metric event (should match the <see cref="Guid"/> returned from the <see cref="IMetricLogger.Begin(IntervalMetric)"/> method).</summary>
            protected Guid beginId;

            /// <summary>
            /// A unique id representing the starting of the interval metric event (should match the <see cref="Guid"/> returned from the <see cref="IMetricLogger.Begin(IntervalMetric)"/> method).
            /// </summary>
            public Guid BeginId
            {
                get { return beginId; }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBuffer+UniqueIntervalMetricEventInstance class.
            /// </summary>
            /// <param name="beginId">A unique id representing the starting of the interval metric event (should match the <see cref="Guid"/> returned from the <see cref="IMetricLogger.Begin(IntervalMetric)"/> method).</param>
            /// <param name="intervalMetric">The metric which occurred.</param>
            /// <param name="timePoint">Whether the event represents the start or the end of the interval metric.</param>
            /// <param name="eventTime">The date and time the metric event started, expressed as UTC.</param>
            public UniqueIntervalMetricEventInstance(Guid beginId, IntervalMetric intervalMetric, IntervalMetricEventTimePoint timePoint, System.DateTime eventTime)
                : base(intervalMetric, timePoint, eventTime)
            {
                this.beginId = beginId;
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
