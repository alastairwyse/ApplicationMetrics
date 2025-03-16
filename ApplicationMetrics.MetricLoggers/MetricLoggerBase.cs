/*
 * Copyright 2025 Alastair Wyse (https://github.com/alastairwyse/ApplicationMetrics/)
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
using System.Collections.Concurrent;
using StandardAbstraction;

namespace ApplicationMetrics.MetricLoggers
{
    /// <summary>
    /// Base for implementationas of <see cref="IMetricLogger"/>.  Contains common routines for processing interval metrics.
    /// </summary>
    public abstract class MetricLoggerBase
    {
        /// <summary>The base time unit to use to log interval metrics.</summary>
        protected readonly IntervalMetricBaseTimeUnit intervalMetricBaseTimeUnit;
        /// <summary>Whether to support interleaving when processing interval metrics.  Set to null when the mode has not yet been determined (i.e. before the <see cref="IMetricLogger.End(Guid, IntervalMetric)"/> or <see cref="IMetricLogger.CancelBegin(Guid, IntervalMetric)"/> methods have been called).</summary>
        protected Nullable<Boolean> interleavedIntervalMetricsMode;
        /// Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed(e.g.End() method called before Begin()).
        protected bool intervalMetricChecking;
        /// <summary>Used to measure elapsed time since starting the buffer processor.</summary>
        protected IStopwatch stopWatch;
        /// <summary>The timestamp at which the stopwatch was started.</summary>
        protected System.DateTime startTime;
        /// <summary>The value of the 'Frequency' property of the StopWatch object.</summary>
        protected Int64 stopWatchFrequency;
        /// <summary>Object which provides Guids.</summary>
        protected IGuidProvider guidProvider;
        /// <summary>Whether the methods in the class will be called concurrently from multiple threads.</summary>
        private Boolean supportConcurrency;
        /// <summary>Dictionary object to temporarily store the start instance of any received interval metrics (when processing in non-interleaved mode).</summary>
        private Dictionary<Type, IntervalMetricEventInstance> startIntervalMetricEventStore;
        /// <summary>Dictionary object to temporarily store the start instance of any received interval metrics (when processing in interleaved mode).</summary>
        private Dictionary<Guid, IntervalMetricEventInstance> startIntervalMetricUniqueEventStore;
        /// <summary>Dictionary object to temporarily store the start instance of any received interval metrics (when processing in concurrent and non-interleaved mode).</summary>
        private ConcurrentDictionary<Type, IntervalMetricEventInstance> startIntervalMetricConcurrentEventStore;
        /// <summary>Dictionary object to temporarily store the start instance of any received interval metrics (when processing in concurrent and interleaved mode).</summary>
        private ConcurrentDictionary<Guid, IntervalMetricEventInstance> startIntervalMetricUniqueConcurrentEventStore;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBase class.
        /// </summary>
        /// <param name="intervalMetricBaseTimeUnit">The base time unit to use to log interval metrics.</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  Note that this parameter only has an effect when running in 'non-interleaved' mode.</param>
        /// <param name="supportConcurrency">Whether the methods in the class will be called concurrently from multiple threads.</param>
        /// <param name="stopWatch">Used to measure elapsed time since starting the buffer processor.</param>
        /// <param name="guidProvider">Object which provides Guids.</param>
        /// <remarks>The class uses a <see cref="Stopwatch"/> to calculate and log interval metrics.  Since the smallest unit of time supported by Stopwatch is a tick (100 nanoseconds), the smallest level of granularity supported when parameter <paramref name="intervalMetricBaseTimeUnit"/> is set to <see cref="IntervalMetricBaseTimeUnit.Nanosecond"/> is 100 nanoseconds.</remarks>
        protected MetricLoggerBase(IntervalMetricBaseTimeUnit intervalMetricBaseTimeUnit, Boolean intervalMetricChecking, Boolean supportConcurrency, IStopwatch stopWatch, IGuidProvider guidProvider)
        {
            this.intervalMetricBaseTimeUnit = intervalMetricBaseTimeUnit;
            this.interleavedIntervalMetricsMode = null;
            this.intervalMetricChecking = intervalMetricChecking;
            this.stopWatch = stopWatch;
            this.guidProvider = guidProvider;
            stopWatchFrequency = stopWatch.Frequency;
            this.supportConcurrency = supportConcurrency;
            if (supportConcurrency == true)
            {
                startIntervalMetricConcurrentEventStore = new ConcurrentDictionary<Type, IntervalMetricEventInstance>();
                startIntervalMetricUniqueConcurrentEventStore = new ConcurrentDictionary<Guid, IntervalMetricEventInstance>();
            }
            else
            {
                startIntervalMetricEventStore = new Dictionary<Type, IntervalMetricEventInstance>();
                startIntervalMetricUniqueEventStore = new Dictionary<Guid, IntervalMetricEventInstance>();
            }
        }

        #region Private/Protected Methods

        /// <summary>
        /// Processes a <see cref="UniqueIntervalMetricEventInstance"/> with a <see cref="IntervalMetricEventTimePoint.Start">'Start'</see> <see cref="IntervalMetricEventInstance.TimePoint">TimePoint</see>.
        /// </summary>
        /// <param name="intervalMetricEventInstance">The <see cref="UniqueIntervalMetricEventInstance"/> to process.</param>
        protected void ProcessStartIntervalMetricEvent(UniqueIntervalMetricEventInstance intervalMetricEventInstance)
        {
            if (supportConcurrency == true)
            {
                ProcessStartIntervalMetricEventConcurrentImplementation(intervalMetricEventInstance);
            }
            else
            {
                ProcessStartIntervalMetricEventImplementation(intervalMetricEventInstance);
            }
        }

        /// <summary>
        /// Processes a <see cref="UniqueIntervalMetricEventInstance"/> with a <see cref="IntervalMetricEventTimePoint.End">'End'</see> <see cref="IntervalMetricEventInstance.TimePoint">TimePoint</see>.
        /// </summary>
        /// <param name="intervalMetricEventInstance">The <see cref="UniqueIntervalMetricEventInstance"/> to process.</param>
        /// <returns>A tuple containing 2 values: the <see cref="IntervalMetricEventInstance"/> generated as a result of the metric <see cref="IntervalMetricEventTimePoint.End">end</see>, and the duration of the interval metric.  Returns null if no metric was generated (e.g. in the case constructor parameter 'intervalMetricChecking' was set to false).</returns>
        protected Tuple<IntervalMetricEventInstance, Int64> ProcessEndIntervalMetricEvent(UniqueIntervalMetricEventInstance intervalMetricEventInstance)
        {
            if (supportConcurrency == true)
            {
                return ProcessEndIntervalMetricEventConcurrentImplementation(intervalMetricEventInstance);
            }
            else
            {
                return ProcessEndIntervalMetricEventImplementation(intervalMetricEventInstance);
            }
        }

        /// <summary>
        /// Processes a <see cref="UniqueIntervalMetricEventInstance"/> with a <see cref="IntervalMetricEventTimePoint.Cancel">'Cancel'</see> <see cref="IntervalMetricEventInstance.TimePoint">TimePoint</see>.
        /// </summary>
        /// <param name="intervalMetricEventInstance">The <see cref="UniqueIntervalMetricEventInstance"/> to process.</param>
        protected void ProcessCancelIntervalMetricEvent(UniqueIntervalMetricEventInstance intervalMetricEventInstance)
        {
            if (supportConcurrency == true)
            {
                ProcessCancelIntervalMetricEventConcurrentImplementation(intervalMetricEventInstance);
            }
            else
            {
                ProcessCancelIntervalMetricEventImplementation(intervalMetricEventInstance);
            }
        }

        /// <summary>
        /// Returns the current date and time as UTC from the 'stopWatch' property.
        /// </summary>
        /// <returns>The current date and time as UTC.</returns>
        protected System.DateTime GetStopWatchUtcNow()
        {
            Int64 elapsedDateTimeTicks;
            if (stopWatchFrequency == 10000000)
            {
                // On every system I've tested the StopWatch.Frequency property on, it's returned 10,000,000
                //   Guessing this is maybe an upper limit of the property (since there's arguably not much point in supporting a frequency greated than the DateTime.Ticks resolution which is also 10,000,000/sec)
                //   In any case, assuming the value is 10,000,000 on many systems, adding this shortcut to avoid conversion to double and overflow handling
                elapsedDateTimeTicks = stopWatch.ElapsedTicks;
            }
            else
            {
                Double stopWatchTicksPerDateTimeTick = 10000000.0 / Convert.ToDouble(stopWatchFrequency);
                Double elapsedDateTimeTicksDouble = stopWatchTicksPerDateTimeTick * Convert.ToDouble(stopWatch.ElapsedTicks);
                try
                {
                    // Would like to not prevent overflow with a try/catch, but can't find any better way to do this
                    //   Chance should be extremely low of ever hitting the catch block... time since starting the stopwatch would have to be > 29,000 years
                    elapsedDateTimeTicks = Convert.ToInt64(elapsedDateTimeTicksDouble);
                }
                catch (OverflowException)
                {
                    elapsedDateTimeTicks = Int64.MaxValue;
                }
            }

            if ((System.DateTime.MaxValue - startTime).Ticks < elapsedDateTimeTicks)
            {
                return System.DateTime.MaxValue.ToUniversalTime();
            }
            else
            {
                return startTime.AddTicks(elapsedDateTimeTicks);
            }
        }

        /// <summary>
        /// Processes a <see cref="UniqueIntervalMetricEventInstance"/> with a <see cref="IntervalMetricEventTimePoint.Start">'Start'</see> <see cref="IntervalMetricEventInstance.TimePoint">TimePoint</see>.
        /// </summary>
        /// <param name="intervalMetricEventInstance">The <see cref="UniqueIntervalMetricEventInstance"/> to process.</param>
        private void ProcessStartIntervalMetricEventImplementation(UniqueIntervalMetricEventInstance intervalMetricEventInstance)
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
        /// Processes a <see cref="UniqueIntervalMetricEventInstance"/> with a <see cref="IntervalMetricEventTimePoint.Start">'Start'</see> <see cref="IntervalMetricEventInstance.TimePoint">TimePoint</see>, supporting concurrent calls from multiple threads.
        /// </summary>
        /// <param name="intervalMetricEventInstance">The <see cref="UniqueIntervalMetricEventInstance"/> to process.</param>
        private void ProcessStartIntervalMetricEventConcurrentImplementation(UniqueIntervalMetricEventInstance intervalMetricEventInstance)
        {
            // Need to handle the case that 'interleavedIntervalMetricsMode' has not yet been set
            if (interleavedIntervalMetricsMode.HasValue == false || interleavedIntervalMetricsMode == false)
            {
                if (startIntervalMetricConcurrentEventStore.ContainsKey(intervalMetricEventInstance.MetricType) && intervalMetricChecking == true)
                {
                    // If a start interval event of this type was already received and checking is enabled, throw an exception
                    throw new InvalidOperationException($"Received duplicate begin '{intervalMetricEventInstance.Metric.Name}' metrics.");
                }
                else
                {
                    startIntervalMetricConcurrentEventStore[intervalMetricEventInstance.MetricType] = intervalMetricEventInstance;
                }
            }
            if (interleavedIntervalMetricsMode.HasValue == false || interleavedIntervalMetricsMode == true)
            {
                // Not checking for a false return value here, since the 'BeginId' property should always be a unique Guid, and hence should never already exist in the dictionary
                startIntervalMetricUniqueConcurrentEventStore.TryAdd(intervalMetricEventInstance.BeginId, intervalMetricEventInstance);
            }
        }

        /// <summary>
        /// Processes a <see cref="UniqueIntervalMetricEventInstance"/> with a <see cref="IntervalMetricEventTimePoint.End">'End'</see> <see cref="IntervalMetricEventInstance.TimePoint">TimePoint</see>.
        /// </summary>
        /// <param name="intervalMetricEventInstance">The <see cref="UniqueIntervalMetricEventInstance"/> to process.</param>
        /// <returns>A tuple containing 2 values: the <see cref="IntervalMetricEventInstance"/> generated as a result of the metric <see cref="IntervalMetricEventTimePoint.End">end</see>, and the duration of the interval metric.  Returns null if no metric was generated (e.g. in the case constructor parameter 'intervalMetricChecking' was set to false).</returns>
        private Tuple<IntervalMetricEventInstance, Int64> ProcessEndIntervalMetricEventImplementation(UniqueIntervalMetricEventInstance intervalMetricEventInstance)
        {
            if (interleavedIntervalMetricsMode == false)
            {
                if (startIntervalMetricEventStore.ContainsKey(intervalMetricEventInstance.MetricType) == true)
                {
                    TimeSpan intervalDuration = intervalMetricEventInstance.EventTime.Subtract(startIntervalMetricEventStore[intervalMetricEventInstance.MetricType].EventTime);
                    Int64 intervalDurationTicks = intervalDuration.Ticks;
                    if (intervalDurationTicks < 0)
                    {
                        intervalDurationTicks = 0;
                    }
                    Int64 intervalDurationBaseTimeUnit;
                    if (intervalMetricBaseTimeUnit == IntervalMetricBaseTimeUnit.Millisecond)
                    {
                        intervalDurationBaseTimeUnit = intervalDurationTicks / 10000;
                    }
                    else
                    {
                        intervalDurationBaseTimeUnit = ConvertTicksToNanoSeconds(intervalDurationTicks);
                    }
                    var returnTuple = new Tuple<IntervalMetricEventInstance, Int64>(startIntervalMetricEventStore[intervalMetricEventInstance.MetricType], intervalDurationBaseTimeUnit);
                    startIntervalMetricEventStore.Remove(intervalMetricEventInstance.MetricType);

                    return returnTuple;
                }
                else
                {
                    // If no corresponding begin interval event of this type exists and checking is enabled, throw an exception
                    if (intervalMetricChecking == true)
                    {
                        throw new InvalidOperationException($"Received end '{intervalMetricEventInstance.Metric.Name}' with no corresponding begin interval metric.");
                    }
                    // If checking is not enabled discard the interval event

                    return null;
                }
            }
            else
            {
                if (startIntervalMetricUniqueEventStore.ContainsKey(intervalMetricEventInstance.BeginId) == true)
                {
                    if (startIntervalMetricUniqueEventStore[intervalMetricEventInstance.BeginId].MetricType != intervalMetricEventInstance.MetricType)
                        throw new ArgumentException($"Metric started with BeginId '{intervalMetricEventInstance.BeginId}' was a '{startIntervalMetricUniqueEventStore[intervalMetricEventInstance.BeginId].Metric.Name}' metric, but {nameof(IMetricLogger.End)}() method was called with a '{intervalMetricEventInstance.Metric.Name}' metric.");

                    TimeSpan intervalDuration = intervalMetricEventInstance.EventTime.Subtract(startIntervalMetricUniqueEventStore[intervalMetricEventInstance.BeginId].EventTime);
                    Int64 intervalDurationTicks = intervalDuration.Ticks;
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
                        intervalDurationBaseTimeUnit = ConvertTicksToNanoSeconds(intervalDurationTicks);
                    }
                    var returnTuple = new Tuple<IntervalMetricEventInstance, Int64>(startIntervalMetricUniqueEventStore[intervalMetricEventInstance.BeginId], intervalDurationBaseTimeUnit);
                    startIntervalMetricUniqueEventStore.Remove(intervalMetricEventInstance.BeginId);

                    return returnTuple;
                }
                else
                {
                    throw new InvalidOperationException($"Received end '{intervalMetricEventInstance.Metric.Name}' with {nameof(UniqueIntervalMetricEventInstance.BeginId)} '{intervalMetricEventInstance.BeginId}' with no corresponding begin interval metric.");
                }
            }
        }

        /// <summary>
        /// Processes a <see cref="UniqueIntervalMetricEventInstance"/> with a <see cref="IntervalMetricEventTimePoint.End">'End'</see> <see cref="IntervalMetricEventInstance.TimePoint">TimePoint</see>, supporting concurrent calls from multiple threads..
        /// </summary>
        /// <param name="intervalMetricEventInstance">The <see cref="UniqueIntervalMetricEventInstance"/> to process.</param>
        /// <returns>A tuple containing 2 values: the <see cref="IntervalMetricEventInstance"/> generated as a result of the metric <see cref="IntervalMetricEventTimePoint.End">end</see>, and the duration of the interval metric.  Returns null if no metric was generated (e.g. in the case constructor parameter 'intervalMetricChecking' was set to false).</returns>
        private Tuple<IntervalMetricEventInstance, Int64> ProcessEndIntervalMetricEventConcurrentImplementation(UniqueIntervalMetricEventInstance intervalMetricEventInstance)
        {
            if (interleavedIntervalMetricsMode == false)
            {
                Boolean startIntervalMetricExists = startIntervalMetricConcurrentEventStore.TryRemove(intervalMetricEventInstance.MetricType, out IntervalMetricEventInstance startIntervalMetric);
                if (startIntervalMetricExists == true)
                {
                    TimeSpan intervalDuration = intervalMetricEventInstance.EventTime.Subtract(startIntervalMetric.EventTime);
                    Int64 intervalDurationTicks = intervalDuration.Ticks;
                    if (intervalDurationTicks < 0)
                    {
                        intervalDurationTicks = 0;
                    }
                    Int64 intervalDurationBaseTimeUnit;
                    if (intervalMetricBaseTimeUnit == IntervalMetricBaseTimeUnit.Millisecond)
                    {
                        intervalDurationBaseTimeUnit = intervalDurationTicks / 10000;
                    }
                    else
                    {
                        intervalDurationBaseTimeUnit = ConvertTicksToNanoSeconds(intervalDurationTicks);
                    }
                    var returnTuple = new Tuple<IntervalMetricEventInstance, Int64>(startIntervalMetric, intervalDurationBaseTimeUnit);

                    return returnTuple;
                }
                else
                {
                    // If no corresponding begin interval event of this type exists and checking is enabled, throw an exception
                    if (intervalMetricChecking == true)
                    {
                        throw new InvalidOperationException($"Received end '{intervalMetricEventInstance.Metric.Name}' with no corresponding begin interval metric.");
                    }
                    // If checking is not enabled discard the interval event

                    return null;
                }
            }
            else
            {
                Boolean startIntervalMetricExists = startIntervalMetricUniqueConcurrentEventStore.TryRemove(intervalMetricEventInstance.BeginId, out IntervalMetricEventInstance startIntervalMetric);
                if (startIntervalMetricExists == true)
                {
                    if (startIntervalMetric.MetricType != intervalMetricEventInstance.MetricType)
                        throw new ArgumentException($"Metric started with BeginId '{intervalMetricEventInstance.BeginId}' was a '{startIntervalMetric.Metric.Name}' metric, but {nameof(IMetricLogger.End)}() method was called with a '{intervalMetricEventInstance.Metric.Name}' metric.");

                    TimeSpan intervalDuration = intervalMetricEventInstance.EventTime.Subtract(startIntervalMetric.EventTime);
                    Int64 intervalDurationTicks = intervalDuration.Ticks;
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
                        intervalDurationBaseTimeUnit = ConvertTicksToNanoSeconds(intervalDurationTicks);
                    }
                    var returnTuple = new Tuple<IntervalMetricEventInstance, Int64>(startIntervalMetric, intervalDurationBaseTimeUnit);

                    return returnTuple;
                }
                else
                {
                    throw new InvalidOperationException($"Received end '{intervalMetricEventInstance.Metric.Name}' with {nameof(UniqueIntervalMetricEventInstance.BeginId)} '{intervalMetricEventInstance.BeginId}' with no corresponding begin interval metric.");
                }
            }
        }

        /// <summary>
        /// Processes a <see cref="UniqueIntervalMetricEventInstance"/> with a <see cref="IntervalMetricEventTimePoint.Cancel">'Cancel'</see> <see cref="IntervalMetricEventInstance.TimePoint">TimePoint</see>.
        /// </summary>
        /// <param name="intervalMetricEventInstance">The <see cref="UniqueIntervalMetricEventInstance"/> to process.</param>
        private void ProcessCancelIntervalMetricEventImplementation(UniqueIntervalMetricEventInstance intervalMetricEventInstance)
        {
            if (interleavedIntervalMetricsMode == false)
            {
                if (startIntervalMetricEventStore.ContainsKey(intervalMetricEventInstance.MetricType) == true)
                {
                    startIntervalMetricEventStore.Remove(intervalMetricEventInstance.MetricType);
                }
                else
                {
                    // If no corresponding begin interval event of this type exists and checking is enabled, throw an exception
                    if (intervalMetricChecking == true)
                    {
                        throw new InvalidOperationException("Received cancel '" + intervalMetricEventInstance.Metric.Name + "' with no corresponding begin interval metric.");
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
                    throw new InvalidOperationException($"Received cancel '{intervalMetricEventInstance.Metric.Name}' with {nameof(UniqueIntervalMetricEventInstance.BeginId)} '{intervalMetricEventInstance.BeginId}' with no corresponding begin interval metric.");
                }
            }
        }

        /// <summary>
        /// Processes a <see cref="UniqueIntervalMetricEventInstance"/> with a <see cref="IntervalMetricEventTimePoint.Cancel">'Cancel'</see> <see cref="IntervalMetricEventInstance.TimePoint">TimePoint</see>, supporting concurrent calls from multiple threads..
        /// </summary>
        /// <param name="intervalMetricEventInstance">The <see cref="UniqueIntervalMetricEventInstance"/> to process.</param>
        private void ProcessCancelIntervalMetricEventConcurrentImplementation(UniqueIntervalMetricEventInstance intervalMetricEventInstance)
        {
            if (interleavedIntervalMetricsMode == false)
            {
                Boolean startIntervalMetricExists = startIntervalMetricConcurrentEventStore.TryRemove(intervalMetricEventInstance.MetricType, out IntervalMetricEventInstance startIntervalMetric);
                if (startIntervalMetricExists == false)
                {
                    // If no corresponding begin interval event of this type exists and checking is enabled, throw an exception
                    if (intervalMetricChecking == true)
                    {
                        throw new InvalidOperationException("Received cancel '" + intervalMetricEventInstance.Metric.Name + "' with no corresponding begin interval metric.");
                    }
                    // If checking is not enabled discard the interval event
                }
            }
            else
            {
                Boolean startIntervalMetricExists = startIntervalMetricUniqueConcurrentEventStore.TryRemove(intervalMetricEventInstance.BeginId, out IntervalMetricEventInstance startIntervalMetric);
                if (startIntervalMetricExists == true)
                {
                    if (startIntervalMetric.MetricType != intervalMetricEventInstance.MetricType)
                        throw new ArgumentException($"Metric started with BeginId '{intervalMetricEventInstance.BeginId}' was a '{startIntervalMetric.Metric.Name}' metric, but {nameof(IMetricLogger.CancelBegin)}() method was called with a '{intervalMetricEventInstance.Metric.Name}' metric.");
                }
                else
                {
                    throw new InvalidOperationException($"Received cancel '{intervalMetricEventInstance.Metric.Name}' with {nameof(UniqueIntervalMetricEventInstance.BeginId)} '{intervalMetricEventInstance.BeginId}' with no corresponding begin interval metric.");
                }
            }
        }

        /// <summary>
        /// Converts the specified value in ticks to nanoseconds.
        /// </summary>
        /// <param name="ticksValue">The value in ticks.</param>
        /// <returns>The value converted to nanoseconds.</returns>
        private Int64 ConvertTicksToNanoSeconds(Int64 ticksValue)
        {
            if (ticksValue == 0)
            {
                return ticksValue;
            }
            if ((Int64.MaxValue / ticksValue) < 100)
            {
                // Prevent overflow
                return Int64.MaxValue;
            }
            else
            {
                return ticksValue * 100;
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
    }
}
