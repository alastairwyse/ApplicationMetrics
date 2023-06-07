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
    /// Base class which supports buffering and storing of metric events, and provides a base framework for classes which log aggregates of metric events.
    /// </summary>
    /// <remarks>
    /// <para>Derived classes must implement methods which log defined metric aggregates (e.g. LogCountOverTimeUnitAggregate()).  These methods are called from a worker thread after dequeueing, totalling, and logging the base metric events.</para>
    /// <para>Since version 5.0.0, the class supports an 'interleaved' mode for interval metrics.  This allows multiple interval metrics of the same type to be started concurrently (e.g. to support scenarios where client code is running on multiple concurrent threads).  The <see cref="IMetricLogger.Begin(IntervalMetric)"/>, <see cref="IMetricLogger.End(Guid, IntervalMetric)"/>, and <see cref="IMetricLogger.CancelBegin(Guid, IntervalMetric)"/> methods support an optional <see cref="Guid"/> return value and parameter which is used to associate/link calls to those methods.  For backwards compatability the non-<see cref="Guid"/> versions of these methods are maintained.  The mode (interleaved or non-interleaved) is chosen on the first call to either the <see cref="IMetricLogger.End(Guid, IntervalMetric)"/> or <see cref="IMetricLogger.CancelBegin(Guid, IntervalMetric)"/> methods, selecting interleaved mode if the <see cref="Guid"/> overload versions of the methods are called, and non-interleaved if the non-<see cref="Guid"/> overload versions are called.  Once the mode is set, calling method overloads corresponding to the other mode will throw an exception, so client code must consistently call either the <see cref="Guid"/> or non-<see cref="Guid"/> overloads of these methods.  Non-interleaved mode may be deprecated in future versions, so it is recommended to migrate client code to support interleaved mode.</para>
    /// </remarks>
    public abstract class MetricAggregateLogger : MetricLoggerStorer, IMetricAggregateLogger
    {
        // Containers for metric aggregates
        /// <summary>Container for aggregates which represent the number of occurrences of a count metric within the specified time unit</summary>
        protected List<MetricAggregateContainer<CountMetric>> countOverTimeUnitAggregateDefinitions;
        /// <summary>Container for aggregates which represent the total of an amount metric per instance of a count metric.</summary>
        protected List<MetricAggregateContainer<AmountMetric, CountMetric>> amountOverCountAggregateDefinitions;
        /// <summary>Container for aggregates which represent the total of an amount metric within the specified time unit.</summary>
        protected List<MetricAggregateContainer<AmountMetric>> amountOverTimeUnitAggregateDefinitions;
        /// <summary>Container for aggregates which represent the total of an amount metric divided by the total of another amount metric.</summary>
        protected List<MetricAggregateContainer<AmountMetric, AmountMetric>> amountOverAmountAggregateDefinitions;
        /// <summary>Container for aggregates which represent the total of an interval metric per instance of a count metric.</summary>
        protected List<MetricAggregateContainer<IntervalMetric, CountMetric>> intervalOverAmountAggregateDefinitions;
        /// <summary>Container for aggregates which represent an interval metric as a fraction of the total runtime of the logger.</summary>
        /// <remarks>Note that the TimeUnit member of the MetricAggregateContainer class is not used in this case.</remarks>
        protected List<MetricAggregateContainer<IntervalMetric>> intervalOverTotalRunTimeAggregateDefinitions;

        /// <summary>Object containing utility methods.</summary>
        protected Utilities utilities;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricAggregateLogger class.
        /// </summary>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricBaseTimeUnit">The base time unit to use to log interval metrics.</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  Note that this parameter only has an effect when running in 'non-interleaved' mode.</param>
        /// <remarks>The class uses a <see cref="Stopwatch"/> to calculate and log interval metrics.  Since the smallest unit of time supported by Stopwatch is a tick (100 nanoseconds), the smallest level of granularity supported when constructor parameter 'intervalMetricBaseTimeUnit' is set to <see cref="IntervalMetricBaseTimeUnit.Nanosecond"/> is 100 nanoseconds.</remarks>
        protected MetricAggregateLogger(IBufferProcessingStrategy bufferProcessingStrategy, IntervalMetricBaseTimeUnit intervalMetricBaseTimeUnit, bool intervalMetricChecking)
            : base(bufferProcessingStrategy, intervalMetricBaseTimeUnit, intervalMetricChecking)
        {
            InitialisePrivateMembers();
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricAggregateLogger class.
        /// </summary>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricBaseTimeUnit">The base time unit to use to log interval metrics.</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  Note that this parameter only has an effect when running in 'non-interleaved' mode.</param>
        /// <param name="dateTime">A test (mock) <see cref="IDateTime"/> object.</param>
        /// <param name="stopWatch">A test (mock) <see cref="IStopwatch"/> object.</param>
        /// <param name="guidProvider">A test (mock) <see cref="IGuidProvider"/> object.</param>
        /// <remarks>This constructor is included to facilitate unit testing.</remarks>
        protected MetricAggregateLogger(IBufferProcessingStrategy bufferProcessingStrategy, IntervalMetricBaseTimeUnit intervalMetricBaseTimeUnit, bool intervalMetricChecking, IDateTime dateTime, IStopwatch stopWatch, IGuidProvider guidProvider)
            : base(bufferProcessingStrategy, intervalMetricBaseTimeUnit, intervalMetricChecking, dateTime, stopWatch, guidProvider)
        {
            InitialisePrivateMembers();
        }

        /// <summary>
        /// Starts the buffer processing (e.g. if the implementation of the buffer processing strategy uses a worker thread, this method starts the worker thread).
        /// </summary>
        /// <remarks>Although this method has been deprecated in base classes, in the case of MetricAggregateLogger this Start() method should be called (rather than the Start() on the IBufferProcessingStrategy implementation) as it performs additional initialization specific to MetricAggregateLogger.</remarks>
        public override void Start()
        {
            base.Start();
        }

        /// <inheritdoc/>
        public virtual void DefineMetricAggregate(CountMetric countMetric, TimeUnit timeUnit, string name, string description)
        {
            CheckDuplicateAggregateName(name);
            countOverTimeUnitAggregateDefinitions.Add(new MetricAggregateContainer<CountMetric>(countMetric, timeUnit, name, description));
        }

        /// <inheritdoc/>
        public virtual void DefineMetricAggregate(AmountMetric amountMetric, CountMetric countMetric, string name, string description)
        {
            CheckDuplicateAggregateName(name);
            amountOverCountAggregateDefinitions.Add(new MetricAggregateContainer<AmountMetric, CountMetric>(amountMetric, countMetric, name, description));
        }

        /// <inheritdoc/>
        public virtual void DefineMetricAggregate(AmountMetric amountMetric, TimeUnit timeUnit, string name, string description)
        {
            CheckDuplicateAggregateName(name);
            amountOverTimeUnitAggregateDefinitions.Add(new MetricAggregateContainer<AmountMetric>(amountMetric, timeUnit, name, description));
        }

        /// <inheritdoc/>
        public virtual void DefineMetricAggregate(AmountMetric numeratorAmountMetric, AmountMetric denominatorAmountMetric, string name, string description)
        {
            CheckDuplicateAggregateName(name);
            amountOverAmountAggregateDefinitions.Add(new MetricAggregateContainer<AmountMetric, AmountMetric>(numeratorAmountMetric, denominatorAmountMetric, name, description));
        }

        /// <inheritdoc/>
        public virtual void DefineMetricAggregate(IntervalMetric intervalMetric, CountMetric countMetric, string name, string description)
        {
            CheckDuplicateAggregateName(name);
            intervalOverAmountAggregateDefinitions.Add(new MetricAggregateContainer<IntervalMetric, CountMetric>(intervalMetric, countMetric, name, description));
        }

        /// <inheritdoc/>
        public virtual void DefineMetricAggregate(IntervalMetric intervalMetric, string name, string description)
        {
            CheckDuplicateAggregateName(name);
            intervalOverTotalRunTimeAggregateDefinitions.Add(new MetricAggregateContainer<IntervalMetric>(intervalMetric, TimeUnit.Second, name, description));
        }

        #region Abstract Methods

        /// <summary>
        /// Logs a metric aggregate representing the number of occurrences of a count metric within the specified time unit.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="totalInstances">The number of occurrences of the count metric.</param>
        /// <param name="totalElapsedTimeUnits">The total elapsed time units.</param>
        protected abstract void LogCountOverTimeUnitAggregate(MetricAggregateContainer<CountMetric> metricAggregate, Int64 totalInstances, Int64 totalElapsedTimeUnits);

        /// <summary>
        /// Logs a metric aggregate representing the total of an amount metric per occurrence of a count metric.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="totalAmount">The total of the amount metric.</param>
        /// <param name="totalInstances">The number of occurrences of the count metric.</param>
        protected abstract void LogAmountOverCountAggregate(MetricAggregateContainer<AmountMetric, CountMetric> metricAggregate, Int64 totalAmount, Int64 totalInstances);

        /// <summary>
        /// Logs a metric aggregate respresenting the total of an amount metric within the specified time unit.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="totalAmount">The total of the amount metric.</param>
        /// <param name="totalElapsedTimeUnits">The total elapsed time units.</param>
        protected abstract void LogAmountOverTimeUnitAggregate(MetricAggregateContainer<AmountMetric> metricAggregate, Int64 totalAmount, Int64 totalElapsedTimeUnits);

        /// <summary>
        /// Logs a metric aggregate representing the total of an amount metric divided by the total of another amount metric.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="numeratorTotal">The total of the numerator amount metric.</param>
        /// <param name="denominatorTotal">The total of the denominator amount metric.</param>
        protected abstract void LogAmountOverAmountAggregate(MetricAggregateContainer<AmountMetric, AmountMetric> metricAggregate, Int64 numeratorTotal, Int64 denominatorTotal);

        /// <summary>
        /// Logs a metric aggregate representing the total of an interval metric per occurrence of a count metric.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="totalInterval">The total of the interval metric.</param>
        /// <param name="totalInstances">The number of occurrences of the count metric.</param>
        protected abstract void LogIntervalOverCountAggregate(MetricAggregateContainer<IntervalMetric, CountMetric> metricAggregate, Int64 totalInterval, Int64 totalInstances);

        /// <summary>
        /// Logs a metric aggregate representing the total of an interval metric as a fraction of the total runtime of the logger.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="totalInterval">The total of the interval metric.</param>
        /// <param name="totalRunTime">The total run time of the logger since starting in milliseconds.</param>
        protected abstract void LogIntervalOverTotalRunTimeAggregate(MetricAggregateContainer<IntervalMetric> metricAggregate, Int64 totalInterval, Int64 totalRunTime);

        #endregion

        #region Base Class Method Implementations

        /// <summary>
        /// Dequeues and logs metric events stored in the internal buffer, and logs any defined metric aggregates.
        /// </summary>
        protected override void DequeueAndProcessMetricEvents()
        {
            base.DequeueAndProcessMetricEvents();
            LogCountOverTimeUnitAggregates();
            LogAmountOverCountAggregates();
            LogAmountOverTimeUnitAggregates();
            LogAmountOverAmountAggregates();
            LogIntervalOverCountMetricAggregates();
            LogIntervalOverTotalRunTimeAggregates();
        }

        #endregion

        #region Private/Protected Methods

        /// <summary>
        /// Initialises private members of the class.
        /// </summary>
        private void InitialisePrivateMembers()
        {
            countOverTimeUnitAggregateDefinitions = new List<MetricAggregateContainer<CountMetric>>();
            amountOverCountAggregateDefinitions = new List<MetricAggregateContainer<AmountMetric, CountMetric>>();
            amountOverTimeUnitAggregateDefinitions = new List<MetricAggregateContainer<AmountMetric>>();
            amountOverAmountAggregateDefinitions = new List<MetricAggregateContainer<AmountMetric, AmountMetric>>();
            intervalOverAmountAggregateDefinitions = new List<MetricAggregateContainer<IntervalMetric, CountMetric>>();
            intervalOverTotalRunTimeAggregateDefinitions = new List<MetricAggregateContainer<IntervalMetric>>();
            utilities = new Utilities();
        }

        /// <summary>
        /// Calculates and logs the value of all defined metric aggregates representing the number of occurrences of a count metric within the specified time unit.
        /// </summary>
        private void LogCountOverTimeUnitAggregates()
        {
            foreach (MetricAggregateContainer<CountMetric> currentAggregate in countOverTimeUnitAggregateDefinitions)
            {
                // Calculate the value
                Int64 totalInstances;
                if (countMetricTotals.ContainsKey(currentAggregate.NumeratorMetricType) == true)
                {
                    totalInstances = countMetricTotals[currentAggregate.NumeratorMetricType].TotalCount;
                }
                else
                {
                    totalInstances = 0;
                }

                // Convert the number of elapsed milliseconds since starting to the time unit specified in the aggregate
                double totalElapsedTimeUnits = stopWatch.ElapsedMilliseconds / utilities.ConvertTimeUnitToMilliSeconds(currentAggregate.DenominatorTimeUnit);
                LogCountOverTimeUnitAggregate(currentAggregate, totalInstances, Convert.ToInt64(totalElapsedTimeUnits));
            }
        }

        /// <summary>
        /// Calculates and logs the value of all defined metric aggregates representing the total of an amount metric per occurrence of a count metric.
        /// </summary>
        private void LogAmountOverCountAggregates()
        {
            foreach (MetricAggregateContainer<AmountMetric, CountMetric> currentAggregate in amountOverCountAggregateDefinitions)
            {
                Int64 totalAmount;
                if (amountMetricTotals.ContainsKey(currentAggregate.NumeratorMetricType) == true)
                {
                    totalAmount = amountMetricTotals[currentAggregate.NumeratorMetricType].Total;
                }
                else
                {
                    totalAmount = 0;
                }

                Int64 totalInstances;
                if (countMetricTotals.ContainsKey(currentAggregate.DenominatorMetricType) == true)
                {
                    totalInstances = countMetricTotals[currentAggregate.DenominatorMetricType].TotalCount;
                }
                else
                {
                    totalInstances = 0;
                }

                LogAmountOverCountAggregate(currentAggregate, totalAmount, totalInstances);
            }
        }

        /// <summary>
        /// Calculates and logs the value of all defined metric aggregates representing the total of an amount metric within the specified time unit.
        /// </summary>
        private void LogAmountOverTimeUnitAggregates()
        {
            foreach (MetricAggregateContainer<AmountMetric> currentAggregate in amountOverTimeUnitAggregateDefinitions)
            {
                // Calculate the total
                Int64 totalAmount;
                if (amountMetricTotals.ContainsKey(currentAggregate.NumeratorMetricType) == true)
                {
                    totalAmount = amountMetricTotals[currentAggregate.NumeratorMetricType].Total;
                }
                else
                {
                    totalAmount = 0;
                }

                // Convert the number of elapsed milliseconds since starting to the time unit specified in the aggregate
                double totalElapsedTimeUnits = stopWatch.ElapsedMilliseconds / utilities.ConvertTimeUnitToMilliSeconds(currentAggregate.DenominatorTimeUnit);
                LogAmountOverTimeUnitAggregate(currentAggregate, totalAmount, Convert.ToInt64(totalElapsedTimeUnits));
            }
        }

        /// <summary>
        /// Calculates and logs the value of all defined metric aggregates representing the total of an amount metric divided by the total of another amount metric.
        /// </summary>
        private void LogAmountOverAmountAggregates()
        {
            foreach (MetricAggregateContainer<AmountMetric, AmountMetric> currentAggregate in amountOverAmountAggregateDefinitions)
            {
                Int64 numeratorTotal;
                if (amountMetricTotals.ContainsKey(currentAggregate.NumeratorMetricType) == true)
                {
                    numeratorTotal = amountMetricTotals[currentAggregate.NumeratorMetricType].Total;
                }
                else
                {
                    numeratorTotal = 0;
                }

                Int64 denominatorTotal;
                if (amountMetricTotals.ContainsKey(currentAggregate.DenominatorMetricType) == true)
                {
                    denominatorTotal = amountMetricTotals[currentAggregate.DenominatorMetricType].Total;
                }
                else
                {
                    denominatorTotal = 0;
                }

                LogAmountOverAmountAggregate(currentAggregate, numeratorTotal, denominatorTotal);
            }
        }

        /// <summary>
        /// Calculates and logs the value of all defined metric aggregates representing the total of an interval metric per occurrence of a count metric.
        /// </summary>
        private void LogIntervalOverCountMetricAggregates()
        {
            foreach (MetricAggregateContainer<IntervalMetric, CountMetric> currentAggregate in intervalOverAmountAggregateDefinitions)
            {
                Int64 totalInterval;
                if (intervalMetricTotals.ContainsKey(currentAggregate.NumeratorMetricType) == true)
                {
                    totalInterval = intervalMetricTotals[currentAggregate.NumeratorMetricType].Total;
                }
                else
                {
                    totalInterval = 0;
                }

                Int64 totalInstances;
                if (countMetricTotals.ContainsKey(currentAggregate.DenominatorMetricType) == true)
                {
                    totalInstances = countMetricTotals[currentAggregate.DenominatorMetricType].TotalCount;
                }
                else
                {
                    totalInstances = 0;
                }

                LogIntervalOverCountAggregate(currentAggregate, totalInterval, totalInstances);
            }
        }

        /// <summary>
        /// Calculates and logs the value of all defined metric aggregates representing the total of an interval metric as a fraction of the total runtime of the logger.
        /// </summary>
        private void LogIntervalOverTotalRunTimeAggregates()
        {
            foreach (MetricAggregateContainer<IntervalMetric> currentAggregate in intervalOverTotalRunTimeAggregateDefinitions)
            {
                Int64 totalInterval;
                if (intervalMetricTotals.ContainsKey(currentAggregate.NumeratorMetricType) == true)
                {
                    totalInterval = intervalMetricTotals[currentAggregate.NumeratorMetricType].Total;
                }
                else
                {
                    totalInterval = 0;
                }
                if (intervalMetricBaseTimeUnit == IntervalMetricBaseTimeUnit.Millisecond)
                {
                    LogIntervalOverTotalRunTimeAggregate(currentAggregate, totalInterval, stopWatch.ElapsedMilliseconds);
                }
                else
                {
                    LogIntervalOverTotalRunTimeAggregate(currentAggregate, totalInterval, stopWatch.ElapsedTicks * 100);
                }
            }
        }

        /// <summary>
        /// Checks all aggregate containers for an existing defined aggregate with the specified name, and throws an exception if an existing aggregate is found.
        /// </summary>
        /// <param name="name">The aggregate name to check for.</param>
        private void CheckDuplicateAggregateName(string name)
        {
            bool exists = false;

            foreach (MetricAggregateContainer<CountMetric> currentAggregate in countOverTimeUnitAggregateDefinitions)
            {
                if (currentAggregate.Name == name)
                {
                    exists = true;
                }
            }
            foreach (MetricAggregateContainer<AmountMetric, CountMetric> currentAggregate in amountOverCountAggregateDefinitions)
            {
                if (currentAggregate.Name == name)
                {
                    exists = true;
                }
            }
            foreach (MetricAggregateContainer<AmountMetric> currentAggregate in amountOverTimeUnitAggregateDefinitions)
            {
                if (currentAggregate.Name == name)
                {
                    exists = true;
                }
            }
            foreach (MetricAggregateContainer<AmountMetric, AmountMetric> currentAggregate in amountOverAmountAggregateDefinitions)
            {
                if (currentAggregate.Name == name)
                {
                    exists = true;
                }
            }
            foreach (MetricAggregateContainer<IntervalMetric, CountMetric> currentAggregate in intervalOverAmountAggregateDefinitions)
            {
                if (currentAggregate.Name == name)
                {
                    exists = true;
                }
            }
            foreach (MetricAggregateContainer<IntervalMetric> currentAggregate in intervalOverTotalRunTimeAggregateDefinitions)
            {
                if (currentAggregate.Name == name)
                {
                    exists = true;
                }
            }

            if (exists == true)
            {
                throw new Exception("Metric aggregate with name '" + name + "' has already been defined.");
            }
        }

        #endregion

        #region Nested Classes

        /// <summary>
        /// Base class for metric aggregate containers containing common properties.
        /// </summary>
        /// <typeparam name="TNumerator">The type representing the numerator of the aggregate.</typeparam>
        protected class MetricAggregateContainerBase<TNumerator>
        {
            /// <summary>The metric representing the numerator of the aggregate.</summary>
            protected TNumerator numeratorMetric;
            /// <summary>The name of the metric aggregate.</summary>
            protected string name;
            /// <summary>A description of the metric aggregate, explaining what it measures and/or represents.</summary>
            protected string description;

            /// <summary>
            /// The type of the numerator of the metric aggregate.
            /// </summary>
            public Type NumeratorMetricType
            {
                get
                {
                    return numeratorMetric.GetType();
                }
            }

            /// <summary>
            /// The name of the metric aggregate.
            /// </summary>
            public string Name
            {
                get
                {
                    return name;
                }
            }

            /// <summary>
            /// A description of the metric aggregate, explaining what it measures and/or represents.
            /// </summary>
            public string Description
            {
                get
                {
                    return description;
                }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricAggregateLogger+MetricAggregateContainerBase class.
            /// </summary>
            /// <param name="numeratorMetric">The metric which is the numerator of the aggregate.</param>
            /// <param name="name">The name of the metric aggregate.</param>
            /// <param name="description">A description of the metric aggregate, explaining what it measures and/or represents.</param>
            protected MetricAggregateContainerBase(TNumerator numeratorMetric, string name, string description)
            {
                this.numeratorMetric = numeratorMetric;

                if (name.Trim() != "")
                {
                    this.name = name;
                }
                else
                {
                    throw new ArgumentException("Argument 'name' cannot be blank.", "name");
                }

                if (description.Trim() != "")
                {
                    this.description = description;
                }
                else
                {
                    throw new ArgumentException("Argument 'description' cannot be blank.", "description");
                }
            }
        }

        /// <summary>
        /// Container class which stores definitions of aggregates of metrics.
        /// </summary>
        /// <typeparam name="TNumerator">The type of the numerator of the metric aggregate.</typeparam>
        /// <typeparam name="TDenominator">The type of the denominator of the metric aggregate.</typeparam>
        protected class MetricAggregateContainer<TNumerator, TDenominator> : MetricAggregateContainerBase<TNumerator>
        {
            /// <summary>The metric representing the denominator of the aggregate.</summary>
            protected TDenominator denominatorMetric;

            /// <summary>
            /// The type of the denominator of the metric aggregate.
            /// </summary>
            public Type DenominatorMetricType
            {
                get
                {
                    return denominatorMetric.GetType();
                }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricAggregateLogger+MetricAggregateContainer class.
            /// </summary>
            /// <param name="numeratorMetric">The metric which is the numerator of the aggregate.</param>
            /// <param name="denominatorMetric">The metric which is the denominator of the aggregate.</param>
            /// <param name="name">The name of the metric aggregate.</param>
            /// <param name="description">A description of the metric aggregate, explaining what it measures and/or represents.</param>
            public MetricAggregateContainer(TNumerator numeratorMetric, TDenominator denominatorMetric, string name, string description)
                : base(numeratorMetric, name, description)
            {
                this.denominatorMetric = denominatorMetric;
            }
        }

        /// <summary>
        /// Container class which stores definitions of aggregates of metrics where the denominator of the metric is a unit of time.
        /// </summary>
        /// <typeparam name="TNumerator">The type of the numerator of the metric aggregate.</typeparam>
        protected class MetricAggregateContainer<TNumerator> : MetricAggregateContainerBase<TNumerator>
        {
            /// <summary>The time unit representing the denominator of the metric aggregate.</summary>
            protected TimeUnit timeUnit;

            /// <summary>
            /// The time unit representing the denominator of the metric aggregate.
            /// </summary>
            public TimeUnit DenominatorTimeUnit
            {
                get
                {
                    return timeUnit;
                }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricAggregateLogger+MetricAggregateContainer class.
            /// </summary>
            /// <param name="numeratorMetric">The metric which is the numerator of the aggregate.</param>
            /// <param name="timeUnit">The time unit representing the denominator of the metric aggregate.</param>
            /// <param name="name">The name of the metric aggregate.</param>
            /// <param name="description">A description of the metric aggregate, explaining what it measures and/or represents.</param>
            public MetricAggregateContainer(TNumerator numeratorMetric, TimeUnit timeUnit, string name, string description)
                : base(numeratorMetric, name, description)
            {
                this.timeUnit = timeUnit;
            }
        }

        #endregion
    }
}
