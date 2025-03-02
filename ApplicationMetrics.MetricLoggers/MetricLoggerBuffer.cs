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
    public abstract class MetricLoggerBuffer : MetricLoggerBase, IMetricLogger, IDisposable
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
        /// <summary>The delegate to handle when a BufferProcessed event is raised.</summary>
        protected readonly EventHandler bufferProcessedEventHandler;
        /// <summary>Object which provides the current date and time.</summary>
        protected readonly IDateTime dateTime;
        /// <summary>Indicates whether the object has been disposed.</summary>
        protected bool disposed;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBuffer class.
        /// </summary>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricBaseTimeUnit">The base time unit to use to log interval metrics.</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  Note that this parameter only has an effect when running in 'non-interleaved' mode.</param>
        /// <remarks>The class uses a <see cref="Stopwatch"/> to calculate and log interval metrics.  Since the smallest unit of time supported by Stopwatch is a tick (100 nanoseconds), the smallest level of granularity supported when parameter <paramref name="intervalMetricBaseTimeUnit"/> is set to <see cref="IntervalMetricBaseTimeUnit.Nanosecond"/> is 100 nanoseconds.</remarks>
        protected MetricLoggerBuffer(IBufferProcessingStrategy bufferProcessingStrategy, IntervalMetricBaseTimeUnit intervalMetricBaseTimeUnit, bool intervalMetricChecking)
            : base(intervalMetricBaseTimeUnit, intervalMetricChecking, new Stopwatch(), new DefaultGuidProvider())
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
            bufferProcessedEventHandler = delegate(object sender, EventArgs e) { DequeueAndProcessMetricEvents(); };
            this.bufferProcessingStrategy.BufferProcessed += bufferProcessedEventHandler;
            dateTime = new StandardAbstraction.DateTime();
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBuffer class.
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
            base.stopWatch = stopWatch;
            base.guidProvider = guidProvider;
            stopWatchFrequency = stopWatch.Frequency;
            this.dateTime = dateTime;
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
                        Tuple<IntervalMetricEventInstance, Int64> generatedInstance = ProcessEndIntervalMetricEvent(currentIntervalMetricEvent);
                        if (generatedInstance != null)
                        {
                            intervalMetricsAndDurations.Enqueue(generatedInstance);
                        }
                        break;

                    // If the current interval metric represents the cancelling of the interval, remove it from the dictionary object 
                    case IntervalMetricEventTimePoint.Cancel:
                        ProcessCancelIntervalMetricEvent(currentIntervalMetricEvent);
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
