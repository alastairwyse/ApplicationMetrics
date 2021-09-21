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
using System.Collections.Generic;
using StandardAbstraction;

namespace ApplicationMetrics.MetricLoggers
{
    /// <summary>
    /// Base class which buffers and totals instances of metric events.
    /// </summary>
    /// <remarks>Derived classes must implement methods which log the totals of metric events (e.g. LogCountMetricTotal()).  These methods are called from a worker thread after dequeueing the buffered metric events and calculating and storing their totals.</remarks>
    public abstract class MetricLoggerStorer : MetricLoggerBuffer
    {
        /// <summary>Dictionary which stores the type of a count metric, and a container object holding the number of instances of that type of count metric event.</summary>
        protected Dictionary<Type, CountMetricTotalContainer> countMetricTotals;
        /// <summary>Dictionary which stores the type of an amount metric, and a container object holding the total amount of all instances of that type of amount metric event.</summary>
        protected Dictionary<Type, AmountMetricTotalContainer> amountMetricTotals;
        /// <summary>Dictionary which stores the type of a status metric, and the most recently logged value of that type of status metric. </summary>
        protected Dictionary<Type, StatusMetricValueContainer> statusMetricLatestValues;
        /// <summary>Dictionary which stores the type of an interval metric, and a container object holding the total amount of all instances of that type of interval metric event.</summary>
        protected Dictionary<Type, IntervalMetricTotalContainer> intervalMetricTotals;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerStorer class.
        /// </summary>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).</param>
        protected MetricLoggerStorer(IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking)
            : base(bufferProcessingStrategy, intervalMetricChecking)
        {
            InitialisePrivateMembers();
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerStorer class.  Note this is an additional constructor to facilitate unit tests, and should not be used to instantiate the class under normal conditions.
        /// </summary>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).</param>
        /// <param name="dateTime">A test (mock) DateTime object.</param>
        /// <param name="stopWatch">A test (mock) Stopwatch object.</param>
        protected MetricLoggerStorer(IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking, IDateTime dateTime, IStopwatch stopWatch)
            : base(bufferProcessingStrategy, intervalMetricChecking, dateTime, stopWatch)
        {
            InitialisePrivateMembers();
        }

        #region Abstract Methods

        /// <summary>
        /// Logs the total number of occurrences of a count metric.
        /// </summary>
        /// <param name="countMetric">The count metric.</param>
        /// <param name="value">The total number of occurrences.</param>
        protected abstract void LogCountMetricTotal(CountMetric countMetric, Int64 value);

        /// <summary>
        /// Logs the total amount of all occurrences of an amount metric.
        /// </summary>
        /// <param name="amountMetric">The amount metric.</param>
        /// <param name="value">The total.</param>
        protected abstract void LogAmountMetricTotal(AmountMetric amountMetric, Int64 value);

        /// <summary>
        /// Logs the most recent value of a status metric.
        /// </summary>
        /// <param name="statusMetric">The status metric.</param>
        /// <param name="value">The value.</param>
        protected abstract void LogStatusMetricValue(StatusMetric statusMetric, Int64 value);

        /// <summary>
        /// Logs the total amount of all occurrences of an interval metric.
        /// </summary>
        /// <param name="intervalMetric">The interval metric.</param>
        /// <param name="value">The total.</param>
        protected abstract void LogIntervalMetricTotal(IntervalMetric intervalMetric, Int64 value);

        #endregion

        #region Base Class Method Implementations

        /// <summary>
        /// Dequeues and logs metric events stored in the internal buffer.
        /// </summary>
        protected override void DequeueAndProcessMetricEvents()
        {
            base.DequeueAndProcessMetricEvents();
            LogCountMetricTotals();
            LogAmountMetricTotals();
            LogStatusMetricValues();
            LogIntervalMetricTotals();
        }

        /// <summary>
        /// Adds the specified count metric events to the stored total.
        /// </summary>
        /// <param name="countMetricEvents">The count metric events.</param>
        protected override void ProcessCountMetricEvents(Queue<CountMetricEventInstance> countMetricEvents)
        {
            while (countMetricEvents.Count > 0)
            {
                CountMetricEventInstance currentCountMetricEvent = countMetricEvents.Dequeue();
                if (countMetricTotals.ContainsKey(currentCountMetricEvent.MetricType) == false)
                {
                    countMetricTotals.Add(currentCountMetricEvent.MetricType, new CountMetricTotalContainer(currentCountMetricEvent.Metric));
                }
                countMetricTotals[currentCountMetricEvent.MetricType].Increment();
            }
        }

        /// <summary>
        /// Adds the values contained in the specified amount metric events to the stored totals.
        /// </summary>
        /// <param name="amountMetricEvents">The amount metric events.</param>
        protected override void ProcessAmountMetricEvents(Queue<AmountMetricEventInstance> amountMetricEvents)
        {
            while (amountMetricEvents.Count > 0)
            {
                AmountMetricEventInstance currentAmountMetricEvent = amountMetricEvents.Dequeue();
                if (amountMetricTotals.ContainsKey(currentAmountMetricEvent.MetricType) == false)
                {
                    amountMetricTotals.Add(currentAmountMetricEvent.MetricType, new AmountMetricTotalContainer(currentAmountMetricEvent.Metric));
                }
                amountMetricTotals[currentAmountMetricEvent.MetricType].Add(currentAmountMetricEvent.Metric.Amount);
            }
        }

        /// <summary>
        /// Stores the values contained in the specified status metric events.
        /// </summary>
        /// <param name="statusMetricEvents">The status metric events.</param>
        protected override void ProcessStatusMetricEvents(Queue<StatusMetricEventInstance> statusMetricEvents)
        {
            while (statusMetricEvents.Count > 0)
            {
                StatusMetricEventInstance currentStatustMetricEvent = statusMetricEvents.Dequeue();
                if (statusMetricLatestValues.ContainsKey(currentStatustMetricEvent.MetricType) == false)
                {
                    statusMetricLatestValues.Add(currentStatustMetricEvent.MetricType, new StatusMetricValueContainer(currentStatustMetricEvent.Metric));
                }
                statusMetricLatestValues[currentStatustMetricEvent.MetricType].Set(currentStatustMetricEvent.Metric.Value);
            }
        }

        /// <summary>
        /// Adds the durations for the specified interval metric events to the stored totals.
        /// </summary>
        /// <param name="intervalMetricEventsAndDurations">The interval metric events and corresponding durations.</param>
        protected override void ProcessIntervalMetricEvents(Queue<Tuple<IntervalMetricEventInstance, Int64>> intervalMetricEventsAndDurations)
        {
            while (intervalMetricEventsAndDurations.Count > 0)
            {
                Tuple<IntervalMetricEventInstance, Int64> currentIntervalMetricEventAndDuration = intervalMetricEventsAndDurations.Dequeue();
                IntervalMetricEventInstance currentIntervalMetricEvent = currentIntervalMetricEventAndDuration.Item1;
                Int64 currentDuration = currentIntervalMetricEventAndDuration.Item2;
                if (intervalMetricTotals.ContainsKey(currentIntervalMetricEvent.MetricType) == false)
                {
                    intervalMetricTotals.Add(currentIntervalMetricEvent.MetricType, new IntervalMetricTotalContainer(currentIntervalMetricEvent.Metric));
                }
                intervalMetricTotals[currentIntervalMetricEvent.MetricType].Add(currentDuration);
            }
        }

        #endregion

        #region Private/Protected Methods

        /// <summary>
        /// Initialises private members of the class.
        /// </summary>
        private void InitialisePrivateMembers()
        {
            countMetricTotals = new Dictionary<Type, CountMetricTotalContainer>();
            amountMetricTotals = new Dictionary<Type, AmountMetricTotalContainer>();
            statusMetricLatestValues = new Dictionary<Type, StatusMetricValueContainer>();
            intervalMetricTotals = new Dictionary<Type, IntervalMetricTotalContainer>();
        }

        /// <summary>
        /// Logs the totals of stored count metrics.
        /// </summary>
        private void LogCountMetricTotals()
        {
            foreach (CountMetricTotalContainer currentCountMetricTotal in countMetricTotals.Values)
            {
                LogCountMetricTotal(currentCountMetricTotal.CountMetric, currentCountMetricTotal.TotalCount);
            }
        }

        /// <summary>
        /// Logs the totals of stored amount metrics.
        /// </summary>
        private void LogAmountMetricTotals()
        {
            foreach (AmountMetricTotalContainer currentAmountMetricTotal in amountMetricTotals.Values)
            {
                LogAmountMetricTotal(currentAmountMetricTotal.AmountMetric, currentAmountMetricTotal.Total);
            }
        }

        /// <summary>
        /// Logs the most recently logged values of stored status metrics.
        /// </summary>
        private void LogStatusMetricValues()
        {
            foreach (StatusMetricValueContainer currentStatusMetricValue in statusMetricLatestValues.Values)
            {
                LogStatusMetricValue(currentStatusMetricValue.StatusMetric, currentStatusMetricValue.Value);
            }
        }

        /// <summary>
        /// Logs the totals of stored interval metrics.
        /// </summary>
        private void LogIntervalMetricTotals()
        {
            foreach (IntervalMetricTotalContainer currentIntervalMetricTotal in intervalMetricTotals.Values)
            {
                LogIntervalMetricTotal(currentIntervalMetricTotal.IntervalMetric, currentIntervalMetricTotal.Total);
            }
        }

        #endregion

        #region Nested Classes

        /// <summary>
        /// Container class which stores an amount metric, and the total amount of all instances of the metric.
        /// </summary>
        protected class AmountMetricTotalContainer
        {
            private AmountMetric amountMetric;
            private Int64 total;

            /// <summary>
            /// The amount metric for which the total is stored.
            /// </summary>
            public AmountMetric AmountMetric
            {
                get
                {
                    return amountMetric;
                }
            }

            /// <summary>
            /// The total amount of all instances of the metric.
            /// </summary>
            public Int64 Total
            {
                get
                {
                    return total;
                }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerStorer+AmountMetricTotalContainer class.
            /// </summary>
            /// <param name="amountMetric">The amount metric for which the total stored.</param>
            public AmountMetricTotalContainer(AmountMetric amountMetric)
            {
                this.amountMetric = amountMetric;
                total = 0;
            }

            /// <summary>
            /// Adds the specified amount to the stored total.
            /// </summary>
            /// <param name="amount">The amount to add.</param>
            public void Add(Int64 amount)
            {
                if ((Int64.MaxValue - total) >= amount)
                {
                    total = total + amount;
                }
            }
        }

        /// <summary>
        /// Container class which stores a count metric, and the total of the number of instances of the metric.
        /// </summary>
        protected class CountMetricTotalContainer
        {
            private CountMetric countMetric;
            private Int64 totalCount;

            /// <summary>
            /// The count metric for which the total number of instances is stored.
            /// </summary>
            public CountMetric CountMetric
            {
                get
                {
                    return countMetric;
                }
            }

            /// <summary>
            /// The total number of instances of the count metric.
            /// </summary>
            public Int64 TotalCount
            {
                get
                {
                    return totalCount;
                }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerStorer+CountMetricTotalContainer class.
            /// </summary>
            /// <param name="countMetric">The count metric for which the total number of instances is stored.</param>
            public CountMetricTotalContainer(CountMetric countMetric)
            {
                this.countMetric = countMetric;
                totalCount = 0;
            }

            /// <summary>
            /// Increments the total number of instances by 1.
            /// </summary>
            public void Increment()
            {
                if (totalCount != Int64.MaxValue)
                {
                    totalCount++;
                }
            }
        }

        /// <summary>
        /// Container class which stores a status metric, and the value of the most recent instance of the metric.
        /// </summary>
        protected class StatusMetricValueContainer
        {
            private StatusMetric statusMetric;
            private Int64 value;

            /// <summary>
            /// The status metric for which the most recent value is stored.
            /// </summary>
            public StatusMetric StatusMetric
            {
                get
                {
                    return statusMetric;
                }
            }

            /// <summary>
            /// The value of the most recent instance of the metric.
            /// </summary>
            public Int64 Value
            {
                get
                {
                    return value;
                }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerStorer+StatusMetricValueContainer class.
            /// </summary>
            /// <param name="statusMetric">The status metric for which the most recent value stored.</param>
            public StatusMetricValueContainer(StatusMetric statusMetric)
            {
                this.statusMetric = statusMetric;
                value = 0;
            }

            /// <summary>
            /// Sets the stored value.
            /// </summary>
            /// <param name="value">The value.</param>
            public void Set(Int64 value)
            {
                this.value = value;
            }
        }

        /// <summary>
        /// Container class which stores an interval metric, and the total interval of all instances of the metric.
        /// </summary>
        protected class IntervalMetricTotalContainer
        {
            private IntervalMetric intervalMetric;
            private Int64 total;

            /// <summary>
            /// The interval metric for which the total is stored.
            /// </summary>
            public IntervalMetric IntervalMetric
            {
                get
                {
                    return intervalMetric;
                }
            }

            /// <summary>
            /// The total interval of all instances of the metric.
            /// </summary>
            public Int64 Total
            {
                get
                {
                    return total;
                }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerStorer+IntervalMetricTotalContainer class.
            /// </summary>
            /// <param name="intervalMetric">The interval metric for which the total stored.</param>
            public IntervalMetricTotalContainer(IntervalMetric intervalMetric)
            {
                this.intervalMetric = intervalMetric;
                total = 0;
            }

            /// <summary>
            /// Adds the specified amount to the stored total.
            /// </summary>
            /// <param name="amount">The amount to add.</param>
            public void Add(Int64 amount)
            {
                if ((Int64.MaxValue - total) >= amount)
                {
                    total = total + amount;
                }
            }
        }

        #endregion
    }
}
