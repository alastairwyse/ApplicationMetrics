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

using System;

namespace ApplicationMetrics.MetricLoggers
{
    /// <summary>
    /// Container class which stores information about the occurrence of an amount metric event.
    /// </summary>
    class AmountMetricEventInstance : MetricEventInstance<AmountMetric>
    {
        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.AmountMetricEventInstance class.
        /// </summary>
        /// <param name="amountMetric">The metric which occurred.</param>
        /// <param name="eventTime">The date and time the metric event occurred, expressed as UTC.</param>
        public AmountMetricEventInstance(AmountMetric amountMetric, DateTime eventTime)
        {
            base.metric = amountMetric;
            base.eventTime = eventTime;
        }
    }
}
