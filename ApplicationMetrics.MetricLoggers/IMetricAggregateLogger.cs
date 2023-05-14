/*
 * Copyright 2014 Alastair Wyse (https://github.com/alastairwyse/ApplicationMetrics/)
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

using System.Drawing;
using System.Xml.Linq;

namespace ApplicationMetrics.MetricLoggers
{
    /// <summary>
    /// Defines methods to register aggregates of metric events, allowing the values of these aggregates to be recorded and logged when the underlying metric events occur.
    /// </summary>
    public interface IMetricAggregateLogger : IMetricLogger
    {
        /// <summary>
        /// Defines a metric aggregate which represents the number of occurrences of a count metric within the specified time unit.
        /// </summary>
        /// <remarks>This metric aggregate could be used to represent the number of messages sent to a remote system each minute, or the number of disk reads per second.</remarks>
        /// <param name="countMetric">The count metric recorded as part of the aggregate.</param>
        /// <param name="timeUnit">The unit of time in which the number of occurrences of the count metric is recorded.</param>
        /// <param name="name">The name of the metric aggregate.</param>
        /// <param name="description">A description of the metric aggregate, explaining what it measures and/or represents.</param>
        void DefineMetricAggregate(CountMetric countMetric, TimeUnit timeUnit, string name, string description);

        /// <summary>
        /// Defines a metric aggregate which represents the total amount of the specified amount metric per occurrence of the specified count metric.
        /// </summary>
        /// <remarks>This metric aggregate could be used to represent the number of bytes per message sent to a remote system, or the number of bytes read per disk read.</remarks>
        /// <param name="amountMetric">The amount metric recorded as part of the aggregate (effectively the numerator).</param>
        /// <param name="countMetric">The count metric per which the total amount of the amount metric(s) are aggregated (effectively the denominator).</param>
        /// <param name="name">The name of the metric aggregate.</param>
        /// <param name="description">A description of the metric aggregate, explaining what it measures and/or represents.</param>
        void DefineMetricAggregate(AmountMetric amountMetric, CountMetric countMetric, string name, string description);

        /// <summary>
        /// Defines a metric aggregate which represents the number of occurrences of a count metric within the specified time unit.
        /// </summary>
        /// <remarks>This metric aggregate could be used to represent the number of messages sent to a remote system each minute, or the number of disk reads per second.</remarks>
        /// <param name="amountMetric">The amount metric recorded as part of the aggregate (effectively the numerator).</param>
        /// <param name="timeUnit">The unit of time in which the amount associated with the specified amount metric is recorded.</param>
        /// <param name="name">The name of the metric aggregate.</param>
        /// <param name="description">A description of the metric aggregate, explaining what it measures and/or represents.</param>
        void DefineMetricAggregate(AmountMetric amountMetric, TimeUnit timeUnit, string name, string description);

        /// <summary>
        /// Defines a metric aggregate which represents the ratio of one amount metric to another.
        /// </summary>
        /// <remarks>This metric aggregate could be used to represent the size of a compressed file against the size of the same file uncompressed, effectively recording the overall compression ratio.</remarks>
        /// <param name="numeratorAmountMetric">The amount metric which is the numerator in the ratio.</param>
        /// <param name="denominatorAmountMetric">The amount metric which is the denominator in the ratio.</param>
        /// <param name="name">The name of the metric aggregate.</param>
        /// <param name="description">A description of the metric aggregate, explaining what it measures and/or represents.</param>
        void DefineMetricAggregate(AmountMetric numeratorAmountMetric, AmountMetric denominatorAmountMetric, string name, string description);

        /// <summary>
        ///  Defines a metric aggregate which represents the total time of the specified interval metric per occurrence of the specified count metric.
        /// </summary>
        /// <remarks>This metric aggregate could be used to represent the average time to send a message to a remote system, or the average time to read perform a disk read.</remarks>
        /// <param name="intervalMetric">The interval metric recorded as part of the aggregate (effectively the numerator).</param>
        /// <param name="countMetric">The count metric per which the total time of the interval metric(s) are aggregated (effectively the denominator).</param>
        /// <param name="name">The name of the metric aggregate.</param>
        /// <param name="description">A description of the metric aggregate, explaining what it measures and/or represents.</param>
        void DefineMetricAggregate(IntervalMetric intervalMetric, CountMetric countMetric, string name, string description);

        /// <summary>
        /// Defines a metric which represents the total time of all occurrences of the specified interval metric as a fraction of the total runtime of the logger.
        /// </summary>
        /// <remarks>This metric aggregate could be used to represent the percentage of total runtime spent sending messages to a remote system.</remarks>
        /// <param name="intervalMetric">The interval metric recorded as part of the aggregate.</param>
        /// <param name="name">The name of the metric aggregate.</param>
        /// <param name="description">A description of the metric aggregate, explaining what it measures and/or represents.</param>
        void DefineMetricAggregate(IntervalMetric intervalMetric, string name, string description);
    }
}
