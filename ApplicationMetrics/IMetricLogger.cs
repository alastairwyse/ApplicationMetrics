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

namespace ApplicationMetrics
{
    /// <summary>
    /// Defines methods to record metric and instrumentation events for an application
    /// </summary>
    public interface IMetricLogger
    {
        /// <summary>
        /// Records a single instance of the specified count event.
        /// </summary>
        /// <param name="countMetric">The count metric that occurred.</param>
        void Increment(CountMetric countMetric);

        /// <summary>
        /// Records an instance of the specified amount metric event, and the associated amount.
        /// </summary>
        /// <param name="amountMetric">The amount metric that occurred.</param>
        /// <param name="amount">The amount associated with the instance of the amount metric.</param>
        void Add(AmountMetric amountMetric, long amount);

        /// <summary>
        /// Records an instance of the specified status metric event, and the associated value.
        /// </summary>
        /// <param name="statusMetric">The status metric that occurred.</param>
        /// <param name="value">The value associated with the instance of the status metric.</param>
        void Set(StatusMetric statusMetric, long value);

        /// <summary>
        /// Records the starting of the specified interval metric event.
        /// </summary>
        /// <param name="intervalMetric">The interval metric that started.</param>
        /// <returns>A unique id for the starting of the interval metric, which should be subsequently passed to the <see cref="IMetricLogger.End(Guid, IntervalMetric)"/> or <see cref="IMetricLogger.CancelBegin(Guid, IntervalMetric)"/> methods, when using the class in interleaved mode.</returns>
        Guid Begin(IntervalMetric intervalMetric);

        /// <summary>
        /// Records the completion of the specified interval metric event when using the class in non-interleaved mode.
        /// </summary>
        /// <param name="intervalMetric">The interval metric that completed.</param>
        void End(IntervalMetric intervalMetric);

        /// <summary>
        /// Records the completion of the specified interval metric event when using the class in interleaved mode.
        /// </summary>
        /// <param name="beginId">The id corresponding to the starting of the specified interval metric event (i.e. returned when the <see cref="IMetricLogger.Begin(IntervalMetric)"/> method was called).</param>
        /// <param name="intervalMetric">The interval metric that completed.</param>
        void End(Guid beginId, IntervalMetric intervalMetric);

        /// <summary>
        /// Cancels the starting of the specified interval metric event when using the class in non-interleaved mode (e.g. in the case that an exception occurs between the starting and completion of the event).
        /// </summary>
        /// <param name="intervalMetric">The interval metric that should be cancelled.</param>
        void CancelBegin(IntervalMetric intervalMetric);

        /// <summary>
        /// Cancels the starting of the specified interval metric event when using the class in interleaved mode (e.g. in the case that an exception occurs between the starting and completion of the event).
        /// </summary>
        /// <param name="beginId">The id corresponding to the starting of the specified interval metric event (i.e. returned when the <see cref="IMetricLogger.Begin(IntervalMetric)"/> method was called).</param>
        /// <param name="intervalMetric">The interval metric that should be cancelled.</param>
        void CancelBegin(Guid beginId, IntervalMetric intervalMetric);
    }
}
