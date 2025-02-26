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

namespace ApplicationMetrics.MetricLoggers
{
    /// <summary>
    /// Base for implementationas of <see cref="IMetricLogger"/>.  Contains common routines for processing interval metrics.
    /// </summary>
    public abstract class MetricLoggerBase
    {
        // Dictionary object to temporarily store the start instance of any received interval metrics (when processing in non-interleaved mode)
        private Dictionary<Type, IntervalMetricEventInstance> startIntervalMetricEventStore;
        // Dictionary object to temporarily store the start instance of any received interval metrics (when processing in interleaved mode)
        private Dictionary<Guid, IntervalMetricEventInstance> startIntervalMetricUniqueEventStore;
        /// <summary>The base time unit to use to log interval metrics.</summary>
        protected readonly IntervalMetricBaseTimeUnit intervalMetricBaseTimeUnit;
        /// <summary>Whether to support interleaving when processing interval metrics.  Set to null when the mode has not yet been determined (i.e. before the <see cref="IMetricLogger.End(Guid, IntervalMetric)"/> or <see cref="IMetricLogger.CancelBegin(Guid, IntervalMetric)"/> methods have been called).</summary>
        protected Nullable<Boolean> interleavedIntervalMetricsMode;
        // Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed(e.g.End() method called before Begin()).
        private bool intervalMetricChecking;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBase class.
        /// </summary>
        /// <param name="intervalMetricBaseTimeUnit">The base time unit to use to log interval metrics.</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  Note that this parameter only has an effect when running in 'non-interleaved' mode.</param>
        /// <remarks>The class uses a <see cref="Stopwatch"/> to calculate and log interval metrics.  Since the smallest unit of time supported by Stopwatch is a tick (100 nanoseconds), the smallest level of granularity supported when parameter <paramref name="intervalMetricBaseTimeUnit"/> is set to <see cref="IntervalMetricBaseTimeUnit.Nanosecond"/> is 100 nanoseconds.</remarks>
        public MetricLoggerBase(IntervalMetricBaseTimeUnit intervalMetricBaseTimeUnit, bool intervalMetricChecking)
        {
            startIntervalMetricEventStore = new Dictionary<Type, IntervalMetricEventInstance>();
            startIntervalMetricUniqueEventStore = new Dictionary<Guid, IntervalMetricEventInstance>();
            this.intervalMetricBaseTimeUnit = intervalMetricBaseTimeUnit;
            this.interleavedIntervalMetricsMode = null;
            this.intervalMetricChecking = intervalMetricChecking;
        }

        #region Private/Protected Methods

        /// <summary>
        /// Processes a <see cref="UniqueIntervalMetricEventInstance"/> with a <see cref="IntervalMetricEventTimePoint.Start">'Start'</see> <see cref="IntervalMetricEventInstance.TimePoint">TimePoint</see> as part of the call the to <see cref="MetricLoggerBuffer.DequeueAndProcessIntervalMetricEvents">DequeueAndProcessIntervalMetricEvents</see>() method.
        /// </summary>
        /// <param name="intervalMetricEventInstance">The <see cref="UniqueIntervalMetricEventInstance"/> to process.</param>
        protected void ProcessStartIntervalMetricEvent(UniqueIntervalMetricEventInstance intervalMetricEventInstance)
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
        /// <returns>A tuple containing 2 values: the <see cref="IntervalMetricEventInstance"/> generated as a result of the metric <see cref="IntervalMetricEventTimePoint.End">end</see>, and the duration of the interval metric.  Returns null if no metric was generated (e.g. in the case constructor parameter 'intervalMetricChecking' was set to false).</returns>
        protected Tuple<IntervalMetricEventInstance, Int64> ProcessEndIntervalMetricEvent(UniqueIntervalMetricEventInstance intervalMetricEventInstance)
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
                    // If no corresponding start interval event of this type exists and checking is enabled, throw an exception
                    if (intervalMetricChecking == true)
                    {
                        throw new InvalidOperationException($"Received end '{intervalMetricEventInstance.Metric.Name}' with no corresponding start interval metric.");
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
                    throw new InvalidOperationException($"Received end '{intervalMetricEventInstance.Metric.Name}' with {nameof(UniqueIntervalMetricEventInstance.BeginId)} '{intervalMetricEventInstance.BeginId}' with no corresponding start interval metric.");
                }
            }
        }

        /// <summary>
        /// Processes an <see cref="UniqueIntervalMetricEventInstance"/> with a <see cref="IntervalMetricEventTimePoint.Cancel">'Cancel'</see> <see cref="IntervalMetricEventInstance.TimePoint">TimePoint</see> as part of the call the to <see cref="MetricLoggerBuffer.DequeueAndProcessIntervalMetricEvents">DequeueAndProcessIntervalMetricEvents</see>() method.
        /// </summary>
        /// <param name="intervalMetricEventInstance">The <see cref="UniqueIntervalMetricEventInstance"/> to process.</param>
        protected void ProcessCancelIntervalMetricEvent(UniqueIntervalMetricEventInstance intervalMetricEventInstance)
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
