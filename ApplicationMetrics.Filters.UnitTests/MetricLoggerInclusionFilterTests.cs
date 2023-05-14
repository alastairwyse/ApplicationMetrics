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
using System.Collections.Generic;
using NUnit.Framework;
using NSubstitute;

namespace ApplicationMetrics.Filters.UnitTests
{
    /// <summary>
    /// Unit tests for class ApplicationMetrics.Filters.MetricLoggerInclusionFilter.
    /// </summary>
    public class MetricLoggerInclusionFilterTests
    {
        private IMetricLogger mockFilteredMetricLogger;
        private MetricLoggerInclusionFilter testMetricLoggerInclusionFilter;

        [SetUp]
        protected void SetUp()
        {
            mockFilteredMetricLogger = Substitute.For<IMetricLogger>();
            testMetricLoggerInclusionFilter = new MetricLoggerInclusionFilter
            (
                mockFilteredMetricLogger,
                new List<CountMetric>() { new DiskReadOperation() },
                new List<AmountMetric>() { new DiskBytesRead() },
                new List<StatusMetric>() { new AvailableMemory() },
                new List<IntervalMetric>() { new DiskReadTime() }
            );
        }

        [Test]
        public void Constructor_IncludedCountMetricsParameterContainsDuplicates()
        {
            var e = Assert.Throws<ArgumentException>(delegate
            {
                testMetricLoggerInclusionFilter = new MetricLoggerInclusionFilter
                (
                    mockFilteredMetricLogger,
                    new List<CountMetric>() { new DiskReadOperation(), new MessageReceived(), new DiskReadOperation() },
                    new List<AmountMetric>(),
                    new List<StatusMetric>(),
                    new List<IntervalMetric>()
                );
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'includedCountMetrics' contains duplicate count metrics of type '{typeof(DiskReadOperation)}'."));
            Assert.AreEqual("includedCountMetrics", e.ParamName);
        }

        [Test]
        public void Constructor_IncludedAmountMetricsParameterContainsDuplicates()
        {
            var e = Assert.Throws<ArgumentException>(delegate
            {
                testMetricLoggerInclusionFilter = new MetricLoggerInclusionFilter
                (
                    mockFilteredMetricLogger,
                    new List<CountMetric>(),
                    new List<AmountMetric>() { new DiskBytesRead(), new MessageBytesReceived(), new DiskBytesRead() },
                    new List<StatusMetric>(),
                    new List<IntervalMetric>()
                );
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'includedAmountMetrics' contains duplicate amount metrics of type '{typeof(DiskBytesRead)}'."));
            Assert.AreEqual("includedAmountMetrics", e.ParamName);
        }

        [Test]
        public void Constructor_IncludedStatusMetricsParameterContainsDuplicates()
        {
            var e = Assert.Throws<ArgumentException>(delegate
            {
                testMetricLoggerInclusionFilter = new MetricLoggerInclusionFilter
                (
                    mockFilteredMetricLogger,
                    new List<CountMetric>(),
                    new List<AmountMetric>(),
                    new List<StatusMetric>() { new AvailableMemory(), new FreeWorkerThreads(), new AvailableMemory() },
                    new List<IntervalMetric>()
                );
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'includedStatusMetrics' contains duplicate status metrics of type '{typeof(AvailableMemory)}'."));
            Assert.AreEqual("includedStatusMetrics", e.ParamName);
        }

        [Test]
        public void Constructor_IncludedIntervalMetricsParameterContainsDuplicates()
        {
            var e = Assert.Throws<ArgumentException>(delegate
            {
                testMetricLoggerInclusionFilter = new MetricLoggerInclusionFilter
                (
                    mockFilteredMetricLogger,
                    new List<CountMetric>(),
                    new List<AmountMetric>(),
                    new List<StatusMetric>(),
                    new List<IntervalMetric>() { new DiskReadTime(), new DiskWriteTime(), new DiskReadTime() }
                );
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'includedIntervalMetrics' contains duplicate interval metrics of type '{typeof(DiskReadTime)}'."));
            Assert.AreEqual("includedIntervalMetrics", e.ParamName);
        }

        [Test]
        public void Increment()
        {
            testMetricLoggerInclusionFilter.Increment(new DiskReadOperation());
            testMetricLoggerInclusionFilter.Increment(new MessageReceived());

            mockFilteredMetricLogger.Received(1).Increment(Arg.Any<DiskReadOperation>());
            mockFilteredMetricLogger.DidNotReceive().Increment(Arg.Any<MessageReceived>());
            Assert.AreEqual(1, mockFilteredMetricLogger.ReceivedCalls().Count());
        }

        [Test]
        public void Add()
        {
            testMetricLoggerInclusionFilter.Add(new DiskBytesRead(), 1);
            testMetricLoggerInclusionFilter.Add(new MessageBytesReceived(), 2);

            mockFilteredMetricLogger.Received(1).Add(Arg.Any<DiskBytesRead>(), 1);
            mockFilteredMetricLogger.DidNotReceive().Add(Arg.Any<MessageBytesReceived>(), 2);
            Assert.AreEqual(1, mockFilteredMetricLogger.ReceivedCalls().Count());
        }

        [Test]
        public void Set()
        {
            testMetricLoggerInclusionFilter.Set(new AvailableMemory(), 3);
            testMetricLoggerInclusionFilter.Set(new FreeWorkerThreads(), 4);

            mockFilteredMetricLogger.Received(1).Set(Arg.Any<AvailableMemory>(), 3);
            mockFilteredMetricLogger.DidNotReceive().Set(Arg.Any<FreeWorkerThreads>(), 4);
            Assert.AreEqual(1, mockFilteredMetricLogger.ReceivedCalls().Count());
        }

        [Test]
        public void Begin()
        {
            Guid testBeginId = Guid.Parse("d993e543-4e11-4c46-a514-5309ce727b2b");
            mockFilteredMetricLogger.Begin(Arg.Any<DiskReadTime>()).Returns<Guid>(testBeginId);

            Guid returnBeginId1 = testMetricLoggerInclusionFilter.Begin(new DiskReadTime());
            Guid returnBeginId2 = testMetricLoggerInclusionFilter.Begin(new DiskWriteTime());

            Assert.AreEqual(testBeginId, returnBeginId1);
            Assert.AreNotEqual(testBeginId, returnBeginId2);
            mockFilteredMetricLogger.Received(1).Begin(Arg.Any<DiskReadTime>());
            mockFilteredMetricLogger.DidNotReceive().Begin(Arg.Any<DiskWriteTime>());
            Assert.AreEqual(1, mockFilteredMetricLogger.ReceivedCalls().Count());
        }

        [Test]
        public void End()
        {
            Guid testBeginId = Guid.Parse("d993e543-4e11-4c46-a514-5309ce727b2b");

            testMetricLoggerInclusionFilter.End(new DiskReadTime());
            testMetricLoggerInclusionFilter.End(new DiskWriteTime());
            testMetricLoggerInclusionFilter.End(testBeginId, new DiskReadTime());
            testMetricLoggerInclusionFilter.End(Guid.NewGuid(), new DiskWriteTime());

            mockFilteredMetricLogger.Received(1).End(Arg.Any<DiskReadTime>());
            mockFilteredMetricLogger.DidNotReceive().End(Arg.Any<DiskWriteTime>());
            mockFilteredMetricLogger.Received(1).End(testBeginId, Arg.Any<DiskReadTime>());
            mockFilteredMetricLogger.DidNotReceive().End(Arg.Any<Guid>(), Arg.Any<DiskWriteTime>());
            Assert.AreEqual(2, mockFilteredMetricLogger.ReceivedCalls().Count());
        }

        [Test]
        public void CancelBegin()
        {
            Guid testBeginId = Guid.Parse("d993e543-4e11-4c46-a514-5309ce727b2b");

            testMetricLoggerInclusionFilter.CancelBegin(new DiskReadTime());
            testMetricLoggerInclusionFilter.CancelBegin(new DiskWriteTime());
            testMetricLoggerInclusionFilter.CancelBegin(testBeginId, new DiskReadTime());
            testMetricLoggerInclusionFilter.CancelBegin(Guid.NewGuid(), new DiskWriteTime());

            mockFilteredMetricLogger.Received(1).CancelBegin(Arg.Any<DiskReadTime>());
            mockFilteredMetricLogger.DidNotReceive().CancelBegin(Arg.Any<DiskWriteTime>());
            mockFilteredMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<DiskReadTime>());
            mockFilteredMetricLogger.DidNotReceive().CancelBegin(Arg.Any<Guid>(), Arg.Any<DiskWriteTime>());
            Assert.AreEqual(2, mockFilteredMetricLogger.ReceivedCalls().Count());
        }
    }
}
