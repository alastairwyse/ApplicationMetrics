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

namespace ApplicationMetrics.MetricLoggers
{
    /// <summary>
    /// Implementation of the IMetricLogger interface which does not perform any metric logging.
    /// </summary>
    /// <remarks>An instance of this class can be used as the default IMetricLogger implementation inside a client class, to prevent occurrences of the 'Object reference not set to an instance of an object' error.</remarks>
    public class NullMetricLogger : IMetricLogger
    {
        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.NullMetricLogger class.
        /// </summary>
        public NullMetricLogger()
        {
        }

        /// <inheritdoc/>
        public void Increment(CountMetric countMetric)
        {
        }

        /// <inheritdoc/>
        public void Add(AmountMetric amountMetric, long amount)
        {
        }

        /// <inheritdoc/>
        public void Set(StatusMetric statusMetric, long value)
        {
        }

        /// <inheritdoc/>
        public Guid Begin(IntervalMetric intervalMetric)
        {
            return Guid.NewGuid();
        }

        /// <inheritdoc/>
        public void End(IntervalMetric intervalMetric)
        {
        }

        /// <inheritdoc/>
        public void End(Guid beginId, IntervalMetric intervalMetric)
        {
        }

        /// <inheritdoc/>
        public void CancelBegin(IntervalMetric intervalMetric)
        {
        }

        /// <inheritdoc/>
        public void CancelBegin(Guid beginId, IntervalMetric intervalMetric)
        {
        }
    }
}
