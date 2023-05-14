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

namespace ApplicationMetrics.Filters
{
    /// <summary>
    /// Filters metrics passed to an <see cref="IMetricLogger"/> instance, by specifying which types of metrics should be logged.  Follows the <see href="https://en.wikipedia.org/wiki/Decorator_pattern">GOF decorator pattern</see>.
    /// </summary>
    public class MetricLoggerTypeFilter : IMetricLogger
    {
        /// <summary>The <see cref="IMetricLogger"/> implementation to filter.</summary>
        protected readonly IMetricLogger filteredMetricLogger;
        /// <summary>Whether or not to log instances of <see cref="CountMetric">CountMetrics</see>.</summary>
        protected readonly Boolean logCountMetrics;
        /// <summary>Whether or not to log instances of <see cref="AmountMetric">AmountMetrics</see>.</summary>
        protected readonly Boolean logAmounttMetrics;
        /// <summary>Whether or not to log instances of <see cref="StatusMetric">StatusMetrics</see>.</summary>
        protected readonly Boolean logStatustMetrics;
        /// <summary>Whether or not to log instances of <see cref="IntervalMetric">IntervalMetrics</see>.</summary>
        protected readonly Boolean logIntervalMetrics;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.Filters.MetricLoggerTypeFilter class.
        /// </summary>
        /// <param name="filteredMetricLogger">The <see cref="IMetricLogger"/> implementation to filter.</param>
        /// <param name="logCountMetrics">Whether or not to log instances of <see cref="CountMetric">CountMetrics</see>.</param>
        /// <param name="logAmounttMetrics">Whether or not to log instances of <see cref="AmountMetric">AmountMetrics</see>.</param>
        /// <param name="logStatustMetrics">Whether or not to log instances of <see cref="StatusMetric">StatusMetrics</see>.</param>
        /// <param name="logIntervalMetrics">Whether or not to log instances of <see cref="IntervalMetric">IntervalMetrics</see>.</param>
        public MetricLoggerTypeFilter(IMetricLogger filteredMetricLogger, Boolean logCountMetrics, Boolean logAmounttMetrics, Boolean logStatustMetrics, Boolean logIntervalMetrics)
        {
            this.filteredMetricLogger = filteredMetricLogger;
            this.logCountMetrics = logCountMetrics;
            this.logAmounttMetrics = logAmounttMetrics;
            this.logStatustMetrics = logStatustMetrics;
            this.logIntervalMetrics = logIntervalMetrics;
        }

        /// <inheritdoc/>
        public void Increment(CountMetric countMetric)
        {
            if (logCountMetrics == true)
            {
                filteredMetricLogger.Increment(countMetric);
            }
        }

        /// <inheritdoc/>
        public void Add(AmountMetric amountMetric, long amount)
        {
            if (logAmounttMetrics == true)
            {
                filteredMetricLogger.Add(amountMetric, amount);
            }
        }

        /// <inheritdoc/>
        public void Set(StatusMetric statusMetric, long value)
        {
            if (logStatustMetrics == true)
            {
                filteredMetricLogger.Set(statusMetric, value);
            }
        }

        /// <inheritdoc/>
        public Guid Begin(IntervalMetric intervalMetric)
        {
            if (logIntervalMetrics == true)
            {
                return filteredMetricLogger.Begin(intervalMetric);
            }
            else
            {
                return Guid.NewGuid();
            }
        }

        /// <inheritdoc/>
        public void End(IntervalMetric intervalMetric)
        {
            if (logIntervalMetrics == true)
            {
                filteredMetricLogger.End(intervalMetric);
            }
        }

        /// <inheritdoc/>
        public void End(Guid beginId, IntervalMetric intervalMetric)
        {
            if (logIntervalMetrics == true)
            {
                filteredMetricLogger.End(beginId, intervalMetric);
            }
        }

        /// <inheritdoc/>
        public void CancelBegin(IntervalMetric intervalMetric)
        {
            if (logIntervalMetrics == true)
            {
                filteredMetricLogger.CancelBegin(intervalMetric);
            }
        }

        /// <inheritdoc/>
        public void CancelBegin(Guid beginId, IntervalMetric intervalMetric)
        {
            if (logIntervalMetrics == true)
            {
                filteredMetricLogger.CancelBegin(beginId, intervalMetric);
            }
        }
    }
}
