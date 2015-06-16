/*
 * Copyright 2014 Alastair Wyse (http://www.oraclepermissiongenerator.net/methodinvocationremoting/)
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

namespace ApplicationMetrics.UnitTests
{
    //******************************************************************************
    //
    // Class: TestMessageBytesReceivedMetric
    //
    //******************************************************************************
    /// <summary>
    /// A sample amount metric for testing implementations of interface IMetricLogger.
    /// </summary>
    class TestMessageBytesReceivedMetric : AmountMetric
    {
        public TestMessageBytesReceivedMetric(long bytesReceived)
        {
            base.name = "MessageBytesReceived";
            base.description = "A single instance of this metric represents the number of bytes received when receiving a message from an external source.";
            base.amount = bytesReceived;
        }
    }
}
