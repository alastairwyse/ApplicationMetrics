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
    /// Unit tests for class ApplicationMetrics.Filters.MetricLoggerExclusionFilter.
    /// </summary>
    public class MetricLoggerExclusionFilterTests
    {
        private IMetricLogger mockFilteredMetricLogger;
        private MetricLoggerExclusionFilter testMetricLoggerExclusionFilter;

        [SetUp]
        protected void SetUp()
        {
            mockFilteredMetricLogger = Substitute.For<IMetricLogger>();
            testMetricLoggerExclusionFilter = new MetricLoggerExclusionFilter
            (
                mockFilteredMetricLogger,
                new List<CountMetric>() { new DiskReadOperation() },
                new List<AmountMetric>() { new DiskBytesRead() },
                new List<StatusMetric>() { new AvailableMemory() },
                new List<IntervalMetric>() { new DiskReadTime() }
            );
        }

        [Test]
        public void Constructor_ExcludedCountMetricsParameterContainsDuplicates()
        {
            var e = Assert.Throws<ArgumentException>(delegate
            {
                testMetricLoggerExclusionFilter = new MetricLoggerExclusionFilter
                (
                    mockFilteredMetricLogger,
                    new List<CountMetric>() { new DiskReadOperation(), new MessageReceived(), new DiskReadOperation() },
                    new List<AmountMetric>(),
                    new List<StatusMetric>(),
                    new List<IntervalMetric>()
                );
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'excludedCountMetrics' contains duplicate count metrics of type '{typeof(DiskReadOperation)}'."));
            Assert.AreEqual("excludedCountMetrics", e.ParamName);
        }

        [Test]
        public void Constructor_ExcludedAmountMetricsParameterContainsDuplicates()
        {
            var e = Assert.Throws<ArgumentException>(delegate
            {
                testMetricLoggerExclusionFilter = new MetricLoggerExclusionFilter
                (
                    mockFilteredMetricLogger,
                    new List<CountMetric>(),
                    new List<AmountMetric>() { new DiskBytesRead(), new MessageBytesReceived(), new DiskBytesRead() },
                    new List<StatusMetric>(),
                    new List<IntervalMetric>()
                );
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'excludedAmountMetrics' contains duplicate amount metrics of type '{typeof(DiskBytesRead)}'."));
            Assert.AreEqual("excludedAmountMetrics", e.ParamName);
        }

        [Test]
        public void Constructor_ExcludedStatusMetricsParameterContainsDuplicates()
        {
            var e = Assert.Throws<ArgumentException>(delegate
            {
                testMetricLoggerExclusionFilter = new MetricLoggerExclusionFilter
                (
                    mockFilteredMetricLogger,
                    new List<CountMetric>(),
                    new List<AmountMetric>(),
                    new List<StatusMetric>() { new AvailableMemory(), new FreeWorkerThreads(), new AvailableMemory() },
                    new List<IntervalMetric>()
                );
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'excludedStatusMetrics' contains duplicate status metrics of type '{typeof(AvailableMemory)}'."));
            Assert.AreEqual("excludedStatusMetrics", e.ParamName);
        }

        [Test]
        public void Constructor_ExcludedIntervalMetricsParameterContainsDuplicates()
        {
            var e = Assert.Throws<ArgumentException>(delegate
            {
                testMetricLoggerExclusionFilter = new MetricLoggerExclusionFilter
                (
                    mockFilteredMetricLogger,
                    new List<CountMetric>(),
                    new List<AmountMetric>(),
                    new List<StatusMetric>(),
                    new List<IntervalMetric>() { new DiskReadTime(), new DiskWriteTime(), new DiskReadTime() }
                );
            });

            Assert.That(e.Message, Does.StartWith($"Parameter 'excludedIntervalMetrics' contains duplicate interval metrics of type '{typeof(DiskReadTime)}'."));
            Assert.AreEqual("excludedIntervalMetrics", e.ParamName);
        }

        [Test]
        public void Increment()
        {
            testMetricLoggerExclusionFilter.Increment(new DiskReadOperation());
            testMetricLoggerExclusionFilter.Increment(new MessageReceived());

            mockFilteredMetricLogger.Received(1).Increment(Arg.Any<MessageReceived>());
            mockFilteredMetricLogger.DidNotReceive().Increment(Arg.Any<DiskReadOperation>());
            Assert.AreEqual(1, mockFilteredMetricLogger.ReceivedCalls().Count());
        }

        [Test]
        public void Add()
        {
            testMetricLoggerExclusionFilter.Add(new DiskBytesRead(), 1);
            testMetricLoggerExclusionFilter.Add(new MessageBytesReceived(), 2);

            mockFilteredMetricLogger.Received(1).Add(Arg.Any<MessageBytesReceived>(), 2);
            mockFilteredMetricLogger.DidNotReceive().Add(Arg.Any<DiskBytesRead>(), 1);
            Assert.AreEqual(1, mockFilteredMetricLogger.ReceivedCalls().Count());
        }

        [Test]
        public void Set()
        {
            testMetricLoggerExclusionFilter.Set(new AvailableMemory(), 3);
            testMetricLoggerExclusionFilter.Set(new FreeWorkerThreads(), 4);

            mockFilteredMetricLogger.Received(1).Set(Arg.Any<FreeWorkerThreads>(), 4);
            mockFilteredMetricLogger.DidNotReceive().Set(Arg.Any<AvailableMemory>(), 3);
            Assert.AreEqual(1, mockFilteredMetricLogger.ReceivedCalls().Count());
        }

        [Test]
        public void Begin()
        {
            Guid testBeginId = Guid.Parse("d993e543-4e11-4c46-a514-5309ce727b2b");
            mockFilteredMetricLogger.Begin(Arg.Any<DiskWriteTime>()).Returns<Guid>(testBeginId);

            Guid returnBeginId1 = testMetricLoggerExclusionFilter.Begin(new DiskReadTime());
            Guid returnBeginId2 = testMetricLoggerExclusionFilter.Begin(new DiskWriteTime());

            Assert.AreNotEqual(testBeginId, returnBeginId1);
            Assert.AreEqual(testBeginId, returnBeginId2);
            mockFilteredMetricLogger.Received(1).Begin(Arg.Any<DiskWriteTime>());
            mockFilteredMetricLogger.DidNotReceive().Begin(Arg.Any<DiskReadTime>());
            Assert.AreEqual(1, mockFilteredMetricLogger.ReceivedCalls().Count());
        }

        [Test]
        public void End()
        {
            Guid testBeginId = Guid.Parse("d993e543-4e11-4c46-a514-5309ce727b2b");

            testMetricLoggerExclusionFilter.End(new DiskReadTime());
            testMetricLoggerExclusionFilter.End(new DiskWriteTime());
            testMetricLoggerExclusionFilter.End(Guid.NewGuid(), new DiskReadTime());
            testMetricLoggerExclusionFilter.End(testBeginId, new DiskWriteTime());

            mockFilteredMetricLogger.Received(1).End(Arg.Any<DiskWriteTime>());
            mockFilteredMetricLogger.DidNotReceive().End(Arg.Any<DiskReadTime>());
            mockFilteredMetricLogger.Received(1).End(testBeginId, Arg.Any<DiskWriteTime>());
            mockFilteredMetricLogger.DidNotReceive().End(Arg.Any<Guid>(), Arg.Any<DiskReadTime>());
            Assert.AreEqual(2, mockFilteredMetricLogger.ReceivedCalls().Count());
        }

        [Test]
        public void CancelBegin()
        {
            Guid testBeginId = Guid.Parse("d993e543-4e11-4c46-a514-5309ce727b2b");

            testMetricLoggerExclusionFilter.CancelBegin(new DiskReadTime());
            testMetricLoggerExclusionFilter.CancelBegin(new DiskWriteTime());
            testMetricLoggerExclusionFilter.CancelBegin(Guid.NewGuid(), new DiskReadTime());
            testMetricLoggerExclusionFilter.CancelBegin(testBeginId, new DiskWriteTime());

            mockFilteredMetricLogger.Received(1).CancelBegin(Arg.Any<DiskWriteTime>());
            mockFilteredMetricLogger.DidNotReceive().CancelBegin(Arg.Any<DiskReadTime>());
            mockFilteredMetricLogger.Received(1).CancelBegin(testBeginId, Arg.Any<DiskWriteTime>());
            mockFilteredMetricLogger.DidNotReceive().CancelBegin(Arg.Any<Guid>(), Arg.Any<DiskReadTime>());
            Assert.AreEqual(2, mockFilteredMetricLogger.ReceivedCalls().Count());
        }
    }
}
