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
    /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="T:ApplicationMetrics.IMetricLogger"]/*'/>
    public interface IMetricLogger
    {
        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.IMetricLogger.Increment(ApplicationMetrics.CountMetric)"]/*'/>
        void Increment(CountMetric countMetric);

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.IMetricLogger.Add(ApplicationMetrics.AmountMetric,System.Int64)"]/*'/>
        void Add(AmountMetric amountMetric, long amount);

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.IMetricLogger.Set(ApplicationMetrics.StatusMetric,System.Int64)"]/*'/>
        void Set(StatusMetric statusMetric, long value);

        // TODO: Fix up xml comments once InterfaceDocumentationComments.xml is properly updated

        // <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.IMetricLogger.Begin(ApplicationMetrics.IntervalMetric)"]/*'/>

        /// <summary>
        /// Records the starting of the specified interval metric event.
        /// </summary>
        /// <param name="intervalMetric">The interval metric that started.</param>
        /// <returns>A unique id for the starting of the interval metric, which should be subsequently passed to the <see cref="IMetricLogger.End(Guid, IntervalMetric)"/> or <see cref="IMetricLogger.CancelBegin(Guid, IntervalMetric)"/> methods, when using the class in interleaved mode.</returns>
        Guid Begin(IntervalMetric intervalMetric);

        // <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.IMetricLogger.End(ApplicationMetrics.IntervalMetric)"]/*'/>


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

        // <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.IMetricLogger.CancelBegin(ApplicationMetrics.IntervalMetric)"]/*'/>

        /// <summary>
        /// Cancels the starting of the specified interval metric event when using the class in non-interleaved mode (e.g. in the case that an exception occurs between the starting and completion of the event).
        /// </summary>
        /// <param name="intervalMetric">The interval metric that should be cancelled.</param>
        void CancelBegin(IntervalMetric intervalMetric);

        /// <summary>
        /// Cancels the starting of the specified interval metric event when using the class in interleaved mode (e.g. in the case that an exception occurs between the starting and completion of the event).
        /// </summary>
        /// <param name="beginId">>The id corresponding to the starting of the specified interval metric event (i.e. returned when the <see cref="IMetricLogger.Begin(IntervalMetric)"/> method was called).</param>
        /// <param name="intervalMetric">The interval metric that should be cancelled.</param>
        void CancelBegin(Guid beginId, IntervalMetric intervalMetric);
    }
}
