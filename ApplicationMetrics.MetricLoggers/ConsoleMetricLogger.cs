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
using StandardAbstraction;

namespace ApplicationMetrics.MetricLoggers
{
    /// <summary>
    /// Writes metric and instrumentation events for an application to the console.
    /// </summary>
    /// <remarks>Since version 5.0.0, the class supports an 'interleaved' mode for interval metrics.  This allows multiple interval metrics of the same type to be started concurrently (e.g. to support scenarios where client code is running on multiple concurrent threads).  The <see cref="IMetricLogger.Begin(IntervalMetric)"/>, <see cref="IMetricLogger.End(Guid, IntervalMetric)"/>, and <see cref="IMetricLogger.CancelBegin(Guid, IntervalMetric)"/> methods support an optional <see cref="Guid"/> return value and parameter which is used to associate/link calls to those methods.  For backwards compatability the non-<see cref="Guid"/> versions of these methods are maintained.  The mode (interleaved or non-interleaved) is chosen on the first call to either the <see cref="IMetricLogger.End(Guid, IntervalMetric)"/> or <see cref="IMetricLogger.CancelBegin(Guid, IntervalMetric)"/> methods, selecting interleaved mode if the <see cref="Guid"/> overload versions of the methods are called, and non-interleaved if the non-<see cref="Guid"/> overload versions are called.  Once the mode is set, calling method overloads corresponding to the other mode will throw an exception, so client code must consistently call either the <see cref="Guid"/> or non-<see cref="Guid"/> overloads of these methods.  Non-interleaved mode may be deprecated in future versions, so it is recommended to migrate client code to support interleaved mode.</remarks>
    public class ConsoleMetricLogger : MetricAggregateLogger
    {
        private const string separatorString = ": ";
        private IConsole console;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.ConsoleMetricLogger class.
        /// </summary>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).</param>
        public ConsoleMetricLogger(IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking)
            : base(bufferProcessingStrategy, intervalMetricChecking)
        {
            console = new StandardAbstraction.Console();
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.ConsoleMetricLogger class.  Note this is an additional constructor to facilitate unit tests, and should not be used to instantiate the class under normal conditions.
        /// </summary>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).</param>
        /// <param name="console">A test (mock) console object.</param>
        /// <param name="dateTime">A test (mock) <see cref="IDateTime"/> object.</param>
        /// <param name="stopWatch">A test (mock) <see cref="IStopwatch"/> object.</param>
        /// <param name="guidProvider">A test (mock) <see cref="IGuidProvider"/> object.</param>
        public ConsoleMetricLogger(IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking, IConsole console, IDateTime dateTime, IStopwatch stopWatch, IGuidProvider guidProvider)
            : base(bufferProcessingStrategy, intervalMetricChecking, dateTime, stopWatch, guidProvider)
        {
            this.console = console;
        }

        #region Base Class Method Implementations

        /// <summary>
        /// Dequeues and logs metric events stored in the internal buffer, and logs any defined metric aggregates.
        /// </summary>
        protected override void DequeueAndProcessMetricEvents()
        {
            console.Clear();
            console.WriteLine("---------------------------------------------------");
            System.DateTime now = dateTime.Now;
            console.WriteLine("-- Application metrics as of " + now.ToString("yyyy-MM-dd HH:mm:ss") + " --");
            console.WriteLine("---------------------------------------------------");
            base.DequeueAndProcessMetricEvents();
        }

        /// <summary>
        /// Logs the total of a count metric to the console.
        /// </summary>
        /// <param name="countMetric">The count metric to log.</param>
        /// <param name="value">The total.</param>
        protected override void LogCountMetricTotal(CountMetric countMetric, Int64 value)
        {
            console.WriteLine(countMetric.Name + separatorString + value.ToString());
        }

        /// <summary>
        /// Logs the total of an amount metric to the console.
        /// </summary>
        /// <param name="amountMetric">The amount metric to log.</param>
        /// <param name="value">The total.</param>
        protected override void LogAmountMetricTotal(AmountMetric amountMetric, Int64 value)
        {
            console.WriteLine(amountMetric.Name + separatorString + value.ToString());
        }

        /// <summary>
        /// Logs the most recent value of a status metric to the console.
        /// </summary>
        /// <param name="statusMetric">The status metric to log.</param>
        /// <param name="value">The value.</param>
        protected override void LogStatusMetricValue(StatusMetric statusMetric, Int64 value)
        {
            console.WriteLine(statusMetric.Name + separatorString + value.ToString());
        }

        /// <summary>
        /// Logs the total of an interval metric to the console.
        /// </summary>
        /// <param name="intervalMetric">The interval metric to log.</param>
        /// <param name="value">The total.</param>
        protected override void LogIntervalMetricTotal(IntervalMetric intervalMetric, Int64 value)
        {
            console.WriteLine(intervalMetric.Name + separatorString + value.ToString());
        }

        /// <summary>
        /// Logs a metric aggregate representing the number of occurrences of a count metric within the specified time unit to the console.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="totalInstances">The number of occurrences of the count metric.</param>
        /// <param name="totalElapsedTimeUnits">The total elapsed time units.</param>
        protected override void LogCountOverTimeUnitAggregate(MetricAggregateContainer<CountMetric> metricAggregate, Int64 totalInstances, Int64 totalElapsedTimeUnits)
        {
            if (totalElapsedTimeUnits != 0)
            {
                double aggregateValue = Convert.ToDouble(totalInstances) / totalElapsedTimeUnits;
                console.WriteLine(metricAggregate.Name + separatorString + aggregateValue);
            }
        }

        /// <summary>
        /// Logs a metric aggregate representing the total of an amount metric per occurrence of a count metric to the console.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="totalAmount">The total of the amount metric.</param>
        /// <param name="totalInstances">The number of occurrences of the count metric.</param>
        protected override void LogAmountOverCountAggregate(MetricAggregateContainer<AmountMetric, CountMetric> metricAggregate, Int64 totalAmount, Int64 totalInstances)
        {
            if (totalInstances != 0)
            {
                double aggregateValue = Convert.ToDouble(totalAmount) / totalInstances;
                console.WriteLine(metricAggregate.Name + separatorString + aggregateValue);
            }
        }

        /// <summary>
        /// Logs a metric aggregate respresenting the total of an amount metric within the specified time unit to the console.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="totalAmount">The total of the amount metric.</param>
        /// <param name="totalElapsedTimeUnits">The total elapsed time units.</param>
        protected override void LogAmountOverTimeUnitAggregate(MetricAggregateContainer<AmountMetric> metricAggregate, Int64 totalAmount, Int64 totalElapsedTimeUnits)
        {
            if (totalElapsedTimeUnits != 0)
            {
                double aggregateValue = Convert.ToDouble(totalAmount) / totalElapsedTimeUnits;
                console.WriteLine(metricAggregate.Name + separatorString + aggregateValue);
            }
        }

        /// <summary>
        /// Logs a metric aggregate respresenting the total of an amount metric divided by the total of another amount metric to the console.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="numeratorTotal">The total of the numerator amount metric.</param>
        /// <param name="denominatorTotal">The total of the denominator amount metric.</param>
        protected override void LogAmountOverAmountAggregate(MetricAggregateContainer<AmountMetric, AmountMetric> metricAggregate, Int64 numeratorTotal, Int64 denominatorTotal)
        {
            if (denominatorTotal != 0)
            {
                double aggregateValue = Convert.ToDouble(numeratorTotal) / denominatorTotal;
                console.WriteLine(metricAggregate.Name + separatorString + aggregateValue);
            }
        }

        /// <summary>
        /// Logs a metric aggregate respresenting the total of an interval metric per occurrence of a count metric to the console.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="totalInterval">The total of the interval metric.</param>
        /// <param name="totalInstances">The number of occurrences of the count metric.</param>
        protected override void LogIntervalOverCountAggregate(MetricAggregateContainer<IntervalMetric, CountMetric> metricAggregate, Int64 totalInterval, Int64 totalInstances)
        {
            if (totalInstances != 0)
            {
                double aggregateValue = Convert.ToDouble(totalInterval) / totalInstances;
                console.WriteLine(metricAggregate.Name + separatorString + aggregateValue);
            }
        }

        /// <summary>
        /// Logs a metric aggregate representing the total of an interval metric as a fraction of the total runtime of the logger to the console.
        /// </summary>
        /// <param name="metricAggregate">The metric aggregate to log.</param>
        /// <param name="totalInterval">The total of the interval metric.</param>
        /// <param name="totalRunTime">The total run time of the logger since starting in milliseonds.</param>
        protected override void LogIntervalOverTotalRunTimeAggregate(MetricAggregateContainer<IntervalMetric> metricAggregate, Int64 totalInterval, Int64 totalRunTime)
        {
            if (totalRunTime > 0)
            {
                double aggregateValue = Convert.ToDouble(totalInterval) / totalRunTime;
                console.WriteLine(metricAggregate.Name + separatorString + aggregateValue);
            }
        }

        #endregion
    }
}
