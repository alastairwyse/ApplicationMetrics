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
using System.Text;
using ApplicationMetrics;

namespace ApplicationMetrics.Filters
{
    /// <summary>
    /// Filters metrics passed to an <see cref="IMetricLogger"/> instance, by specifying which metrics should be allowed to be be logged.  Follows the <see href="https://en.wikipedia.org/wiki/Decorator_pattern">GOF decorator pattern</see>.
    /// </summary>
    public class MetricLoggerInclusionFilter : IMetricLogger
    {
        /// <summary>The <see cref="IMetricLogger"/> implementation to filter.</summary>
        protected readonly IMetricLogger filteredMetricLogger;
        /// <summary>Types of count metrics which should be logged.</summary>
        protected readonly HashSet<Type> includedCountMetrics;
        /// <summary>Types of amount metrics which should be logged.</summary>
        protected readonly HashSet<Type> includedAmountMetrics;
        /// <summary>Types of status metrics which should be logged.</summary>
        protected readonly HashSet<Type> includedStatusMetrics;
        /// <summary>Types of interval metrics which should be logged.</summary>
        protected readonly HashSet<Type> includedIntervalMetrics;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.Filters.MetricLoggerExclusionFilter class.
        /// </summary>
        /// <param name="filteredMetricLogger">The <see cref="IMetricLogger"/> implementation to filter.</param>
        /// <param name="includedCountMetrics">Ccount metrics which should be logged.</param>
        /// <param name="includedAmountMetrics">Amount metrics which should be logged.</param>
        /// <param name="includedStatusMetrics">Status metrics which should be logged.</param>
        /// <param name="includedIntervalMetrics">Interval metrics which should be logged.</param>
        public MetricLoggerInclusionFilter
        (
            IMetricLogger filteredMetricLogger,
            IEnumerable<CountMetric> includedCountMetrics,
            IEnumerable<AmountMetric> includedAmountMetrics,
            IEnumerable<StatusMetric> includedStatusMetrics,
            IEnumerable<IntervalMetric> includedIntervalMetrics
        )
        {
            this.filteredMetricLogger = filteredMetricLogger;
            this.includedCountMetrics = new HashSet<Type>();
            this.includedAmountMetrics = new HashSet<Type>();
            this.includedStatusMetrics = new HashSet<Type>();
            this.includedIntervalMetrics = new HashSet<Type>();
            foreach (CountMetric currentCountMetric in includedCountMetrics)
            {
                if (this.includedCountMetrics.Contains(currentCountMetric.GetType()) == true)
                    throw new ArgumentException($"Parameter '{nameof(includedCountMetrics)}' contains duplicate count metrics of type '{currentCountMetric.GetType()}'.", nameof(includedCountMetrics));
                this.includedCountMetrics.Add(currentCountMetric.GetType());
            }
            foreach (AmountMetric currentAmountMetric in includedAmountMetrics)
            {
                if (this.includedAmountMetrics.Contains(currentAmountMetric.GetType()) == true)
                    throw new ArgumentException($"Parameter '{nameof(includedAmountMetrics)}' contains duplicate amount metrics of type '{currentAmountMetric.GetType()}'.", nameof(includedAmountMetrics));
                this.includedAmountMetrics.Add(currentAmountMetric.GetType());
            }
            foreach (StatusMetric currentStatusMetric in includedStatusMetrics)
            {
                if (this.includedStatusMetrics.Contains(currentStatusMetric.GetType()) == true)
                    throw new ArgumentException($"Parameter '{nameof(includedStatusMetrics)}' contains duplicate status metrics of type '{currentStatusMetric.GetType()}'.", nameof(includedStatusMetrics));
                this.includedStatusMetrics.Add(currentStatusMetric.GetType());
            }
            foreach (IntervalMetric currentIntervalMetric in includedIntervalMetrics)
            {
                if (this.includedIntervalMetrics.Contains(currentIntervalMetric.GetType()) == true)
                    throw new ArgumentException($"Parameter '{nameof(includedIntervalMetrics)}' contains duplicate interval metrics of type '{currentIntervalMetric.GetType()}'.", nameof(includedIntervalMetrics));
                this.includedIntervalMetrics.Add(currentIntervalMetric.GetType());
            }
        }

        /// <inheritdoc/>
        public void Increment(CountMetric countMetric)
        {
            if (includedCountMetrics.Contains(countMetric.GetType()) == true)
            {
                filteredMetricLogger.Increment(countMetric);
            }
        }

        /// <inheritdoc/>
        public void Add(AmountMetric amountMetric, long amount)
        {
            if (includedAmountMetrics.Contains(amountMetric.GetType()) == true)
            {
                filteredMetricLogger.Add(amountMetric, amount);
            }
        }

        /// <inheritdoc/>
        public void Set(StatusMetric statusMetric, long value)
        {
            if (includedStatusMetrics.Contains(statusMetric.GetType()) == true)
            {
                filteredMetricLogger.Set(statusMetric, value);
            }
        }

        /// <inheritdoc/>
        public Guid Begin(IntervalMetric intervalMetric)
        {
            if (includedIntervalMetrics.Contains(intervalMetric.GetType()) == true)
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
            if (includedIntervalMetrics.Contains(intervalMetric.GetType()) == true)
            {
                filteredMetricLogger.End(intervalMetric);
            }
        }

        /// <inheritdoc/>
        public void End(Guid beginId, IntervalMetric intervalMetric)
        {
            if (includedIntervalMetrics.Contains(intervalMetric.GetType()) == true)
            {
                filteredMetricLogger.End(beginId, intervalMetric);
            }
        }

        /// <inheritdoc/>
        public void CancelBegin(IntervalMetric intervalMetric)
        {
            if (includedIntervalMetrics.Contains(intervalMetric.GetType()) == true)
            {
                filteredMetricLogger.CancelBegin(intervalMetric);
            }
        }

        /// <inheritdoc/>
        public void CancelBegin(Guid beginId, IntervalMetric intervalMetric)
        {
            if (includedIntervalMetrics.Contains(intervalMetric.GetType()) == true)
            {
                filteredMetricLogger.CancelBegin(beginId, intervalMetric);
            }
        }
    }
}
