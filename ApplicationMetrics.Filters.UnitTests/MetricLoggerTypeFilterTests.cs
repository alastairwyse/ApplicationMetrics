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
using System.Linq;
using NUnit.Framework;
using NSubstitute;

namespace ApplicationMetrics.Filters.UnitTests
{
    /// <summary>
    /// Unit tests for class ApplicationMetrics.Filters.MetricLoggerTypeFilter.
    /// </summary>
    public class MetricLoggerTypeFilterTests
    {
        private IMetricLogger mockFilteredMetricLogger;
        private MetricLoggerTypeFilter testMetricLoggerTypeFilter;


        [SetUp]
        protected void SetUp()
        {
            mockFilteredMetricLogger = Substitute.For<IMetricLogger>();
        }

        [Test]
        public void Increment()
        {
            testMetricLoggerTypeFilter = new MetricLoggerTypeFilter(mockFilteredMetricLogger, true, true, true, true);

            testMetricLoggerTypeFilter.Increment(new DiskReadOperation());

            mockFilteredMetricLogger.Received(1).Increment(Arg.Any<DiskReadOperation>());
            Assert.AreEqual(1, mockFilteredMetricLogger.ReceivedCalls().Count());


            mockFilteredMetricLogger.ClearReceivedCalls();
            testMetricLoggerTypeFilter = new MetricLoggerTypeFilter(mockFilteredMetricLogger, false, true, true, true);

            testMetricLoggerTypeFilter.Increment(new DiskReadOperation());

            mockFilteredMetricLogger.DidNotReceive().Increment(Arg.Any<DiskReadOperation>());
            Assert.AreEqual(0, mockFilteredMetricLogger.ReceivedCalls().Count());
        }

        [Test]
        public void Add()
        {
            testMetricLoggerTypeFilter = new MetricLoggerTypeFilter(mockFilteredMetricLogger, true, true, true, true);

            testMetricLoggerTypeFilter.Add(new DiskBytesRead(), 1);

            mockFilteredMetricLogger.Received(1).Add(Arg.Any<DiskBytesRead>(), 1);
            Assert.AreEqual(1, mockFilteredMetricLogger.ReceivedCalls().Count());


            mockFilteredMetricLogger.ClearReceivedCalls();
            testMetricLoggerTypeFilter = new MetricLoggerTypeFilter(mockFilteredMetricLogger, true, false, true, true);

            testMetricLoggerTypeFilter.Add(new DiskBytesRead(), 1);

            mockFilteredMetricLogger.DidNotReceive().Add(Arg.Any<DiskBytesRead>(), 1);
            Assert.AreEqual(0, mockFilteredMetricLogger.ReceivedCalls().Count());
        }

        [Test]
        public void Set()
        {
            testMetricLoggerTypeFilter = new MetricLoggerTypeFilter(mockFilteredMetricLogger, true, true, true, true);

            testMetricLoggerTypeFilter.Set(new AvailableMemory(), 2);

            mockFilteredMetricLogger.Received(1).Set(Arg.Any<AvailableMemory>(), 2);
            Assert.AreEqual(1, mockFilteredMetricLogger.ReceivedCalls().Count());


            mockFilteredMetricLogger.ClearReceivedCalls();
            testMetricLoggerTypeFilter = new MetricLoggerTypeFilter(mockFilteredMetricLogger, true, true, false, true);

            testMetricLoggerTypeFilter.Set(new AvailableMemory(), 2);

            mockFilteredMetricLogger.DidNotReceive().Set(Arg.Any<AvailableMemory>(), 2);
            Assert.AreEqual(0, mockFilteredMetricLogger.ReceivedCalls().Count());
        }

        [Test]
        public void Begin()
        {
            testMetricLoggerTypeFilter = new MetricLoggerTypeFilter(mockFilteredMetricLogger, true, true, true, true);

            Guid returnBeginId = testMetricLoggerTypeFilter.Begin(new DiskReadTime());

            mockFilteredMetricLogger.Received(1).Begin(Arg.Any<DiskReadTime>());
            Assert.AreEqual(1, mockFilteredMetricLogger.ReceivedCalls().Count());


            mockFilteredMetricLogger.ClearReceivedCalls();
            testMetricLoggerTypeFilter = new MetricLoggerTypeFilter(mockFilteredMetricLogger, true, true, true, false);

            returnBeginId = testMetricLoggerTypeFilter.Begin(new DiskReadTime());

            mockFilteredMetricLogger.DidNotReceive().Begin(Arg.Any<DiskReadTime>());
            Assert.AreEqual(0, mockFilteredMetricLogger.ReceivedCalls().Count());
        }

        [Test]
        public void End()
        {
            Guid testBeginId = Guid.Parse("d993e543-4e11-4c46-a514-5309ce727b2b");
            testMetricLoggerTypeFilter = new MetricLoggerTypeFilter(mockFilteredMetricLogger, true, true, true, true);

            testMetricLoggerTypeFilter.End(new DiskReadTime());
            testMetricLoggerTypeFilter.End(testBeginId, new DiskReadTime());

            mockFilteredMetricLogger.Received(1).End(Arg.Any<DiskReadTime>());
            mockFilteredMetricLogger.Received(1).End(testBeginId, Arg.Any<DiskReadTime>());
            Assert.AreEqual(2, mockFilteredMetricLogger.ReceivedCalls().Count());


            mockFilteredMetricLogger.ClearReceivedCalls();
            testMetricLoggerTypeFilter = new MetricLoggerTypeFilter(mockFilteredMetricLogger, true, true, true, false);

            testMetricLoggerTypeFilter.End(new DiskReadTime());
            testMetricLoggerTypeFilter.End(testBeginId, new DiskReadTime());

            mockFilteredMetricLogger.DidNotReceive().End(Arg.Any<DiskReadTime>());
            mockFilteredMetricLogger.DidNotReceive().End(testBeginId, Arg.Any<DiskReadTime>());
            Assert.AreEqual(0, mockFilteredMetricLogger.ReceivedCalls().Count());
        }

        [Test]
        public void CancelBegin()
        {
            Guid testBeginId = Guid.Parse("d993e543-4e11-4c46-a514-5309ce727b2b");
            testMetricLoggerTypeFilter = new MetricLoggerTypeFilter(mockFilteredMetricLogger, true, true, true, true);

            testMetricLoggerTypeFilter.CancelBegin(new DiskReadTime());
            testMetricLoggerTypeFilter.CancelBegin(testBeginId, new DiskReadTime());

            mockFilteredMetricLogger.Received(1).CancelBegin(Arg.Any<DiskReadTime>());
            mockFilteredMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<DiskReadTime>());
            Assert.AreEqual(2, mockFilteredMetricLogger.ReceivedCalls().Count());


            mockFilteredMetricLogger.ClearReceivedCalls();
            testMetricLoggerTypeFilter = new MetricLoggerTypeFilter(mockFilteredMetricLogger, true, true, true, false);

            testMetricLoggerTypeFilter.CancelBegin(new DiskReadTime());
            testMetricLoggerTypeFilter.CancelBegin(testBeginId, new DiskReadTime());

            mockFilteredMetricLogger.DidNotReceive().CancelBegin(Arg.Any<DiskReadTime>());
            mockFilteredMetricLogger.DidNotReceive().CancelBegin(testBeginId, Arg.Any<DiskReadTime>());
            Assert.AreEqual(0, mockFilteredMetricLogger.ReceivedCalls().Count());
        }
    }
}
