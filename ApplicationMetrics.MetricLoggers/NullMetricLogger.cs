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

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IMetricLogger.Increment(ApplicationMetrics.CountMetric)"]/*'/>
        public void Increment(CountMetric countMetric)
        {
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IMetricLogger.Add(ApplicationMetrics.AmountMetric,System.Int64)"]/*'/>
        public void Add(AmountMetric amountMetric, long amount)
        {
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.MetricLoggers.IMetricLogger.Set(ApplicationMetrics.StatusMetric,System.Int64)"]/*'/>
        public void Set(StatusMetric statusMetric, long value)
        {
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.IMetricLogger.Begin(ApplicationMetrics.IntervalMetric)"]/*'/>
        public Guid Begin(IntervalMetric intervalMetric)
        {
            return Guid.NewGuid();
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.IMetricLogger.End(ApplicationMetrics.IntervalMetric)"]/*'/>
        public void End(IntervalMetric intervalMetric)
        {
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.IMetricLogger.End(System.Guid,ApplicationMetrics.IntervalMetric)"]/*'/>
        public void End(Guid beginId, IntervalMetric intervalMetric)
        {
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.IMetricLogger.CancelBegin(ApplicationMetrics.IntervalMetric)"]/*'/>
        public void CancelBegin(IntervalMetric intervalMetric)
        {
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.IMetricLogger.CancelBegin(System.Guid,ApplicationMetrics.IntervalMetric)"]/*'/>
        public void CancelBegin(Guid beginId, IntervalMetric intervalMetric)
        {
        }
    }
}
