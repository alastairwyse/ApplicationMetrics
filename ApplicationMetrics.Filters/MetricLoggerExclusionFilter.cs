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

namespace ApplicationMetrics.Filters
{
    /// <summary>
    /// Filters metrics passed to an <see cref="IMetricLogger"/> instance, by specifying which metrics should be prevented from being logged.  Follows the <see href="https://en.wikipedia.org/wiki/Decorator_pattern">GOF decorator pattern</see>.
    /// </summary>
    public class MetricLoggerExclusionFilter : IMetricLogger
    {
        /// <summary>The <see cref="IMetricLogger"/> implementation to filter.</summary>
        protected readonly IMetricLogger filteredMetricLogger;
        /// <summary>Types of count metrics which should be excluded from logging.</summary>
        protected readonly HashSet<Type> excludedCountMetrics;
        /// <summary>Types of amount metrics which should be excluded from logging.</summary>
        protected readonly HashSet<Type> excludedAmountMetrics;
        /// <summary>Types of status metrics which should be excluded from logging.</summary>
        protected readonly HashSet<Type> excludedStatusMetrics;
        /// <summary>Types of interval metrics which should be excluded from logging.</summary>
        protected readonly HashSet<Type> excludedIntervalMetrics;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.Filters.MetricLoggerExclusionFilter class.
        /// </summary>
        /// <param name="filteredMetricLogger">The <see cref="IMetricLogger"/> implementation to filter.</param>
        /// <param name="excludedCountMetrics">Ccount metrics which should be excluded from logging.</param>
        /// <param name="excludedAmountMetrics">Amount metrics which should be excluded from logging.</param>
        /// <param name="excludedStatusMetrics">Status metrics which should be excluded from logging.</param>
        /// <param name="excludedIntervalMetrics">Interval metrics which should be excluded from logging.</param>
        public MetricLoggerExclusionFilter
        (
            IMetricLogger filteredMetricLogger, 
            IEnumerable<CountMetric> excludedCountMetrics,
            IEnumerable<AmountMetric> excludedAmountMetrics, 
            IEnumerable<StatusMetric> excludedStatusMetrics,
            IEnumerable<IntervalMetric> excludedIntervalMetrics
        )
        {
            this.filteredMetricLogger = filteredMetricLogger;
            this.excludedCountMetrics = new HashSet<Type>();
            this.excludedAmountMetrics = new HashSet<Type>();
            this.excludedStatusMetrics = new HashSet<Type>();
            this.excludedIntervalMetrics = new HashSet<Type>();
            foreach (CountMetric currentCountMetric in excludedCountMetrics)
            {
                if (this.excludedCountMetrics.Contains(currentCountMetric.GetType()) == true)
                    throw new ArgumentException($"Parameter '{nameof(excludedCountMetrics)}' contains duplicate count metrics of type '{currentCountMetric.GetType()}'.", nameof(excludedCountMetrics));
                this.excludedCountMetrics.Add(currentCountMetric.GetType());
            }
            foreach (AmountMetric currentAmountMetric in excludedAmountMetrics)
            {
                if (this.excludedAmountMetrics.Contains(currentAmountMetric.GetType()) == true)
                    throw new ArgumentException($"Parameter '{nameof(excludedAmountMetrics)}' contains duplicate amount metrics of type '{currentAmountMetric.GetType()}'.", nameof(excludedAmountMetrics));
                this.excludedAmountMetrics.Add(currentAmountMetric.GetType());
            }
            foreach (StatusMetric currentStatusMetric in excludedStatusMetrics)
            {
                if (this.excludedStatusMetrics.Contains(currentStatusMetric.GetType()) == true)
                    throw new ArgumentException($"Parameter '{nameof(excludedStatusMetrics)}' contains duplicate status metrics of type '{currentStatusMetric.GetType()}'.", nameof(excludedStatusMetrics));
                this.excludedStatusMetrics.Add(currentStatusMetric.GetType());
            }
            foreach (IntervalMetric currentIntervalMetric in excludedIntervalMetrics)
            {
                if (this.excludedIntervalMetrics.Contains(currentIntervalMetric.GetType()) == true)
                    throw new ArgumentException($"Parameter '{nameof(excludedIntervalMetrics)}' contains duplicate interval metrics of type '{currentIntervalMetric.GetType()}'.", nameof(excludedIntervalMetrics));
                this.excludedIntervalMetrics.Add(currentIntervalMetric.GetType());
            }
        }

        /// <inheritdoc/>
        public void Increment(CountMetric countMetric)
        {
            if (excludedCountMetrics.Contains(countMetric.GetType()) == false)
            {
                filteredMetricLogger.Increment(countMetric);
            }
        }

        /// <inheritdoc/>
        public void Add(AmountMetric amountMetric, long amount)
        {
            if (excludedAmountMetrics.Contains(amountMetric.GetType()) == false)
            {
                filteredMetricLogger.Add(amountMetric, amount);
            }
        }

        /// <inheritdoc/>
        public void Set(StatusMetric statusMetric, long value)
        {
            if (excludedStatusMetrics.Contains(statusMetric.GetType()) == false)
            {
                filteredMetricLogger.Set(statusMetric, value);
            }
        }

        /// <inheritdoc/>
        public Guid Begin(IntervalMetric intervalMetric)
        {
            if (excludedIntervalMetrics.Contains(intervalMetric.GetType()) == false)
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
            if (excludedIntervalMetrics.Contains(intervalMetric.GetType()) == false)
            {
                filteredMetricLogger.End(intervalMetric);
            }
        }

        /// <inheritdoc/>
        public void End(Guid beginId, IntervalMetric intervalMetric)
        {
            if (excludedIntervalMetrics.Contains(intervalMetric.GetType()) == false)
            {
                filteredMetricLogger.End(beginId, intervalMetric);
            }
        }

        /// <inheritdoc/>
        public void CancelBegin(IntervalMetric intervalMetric)
        {
            if (excludedIntervalMetrics.Contains(intervalMetric.GetType()) == false)
            {
                filteredMetricLogger.CancelBegin(intervalMetric);
            }
        }

        /// <inheritdoc/>
        public void CancelBegin(Guid beginId, IntervalMetric intervalMetric)
        {
            if (excludedIntervalMetrics.Contains(intervalMetric.GetType()) == false)
            {
                filteredMetricLogger.CancelBegin(beginId, intervalMetric);
            }
        }
    }
}
