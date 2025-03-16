/*
 * Copyright 2025 Alastair Wyse (https://github.com/alastairwyse/ApplicationMetrics/)
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
using System.Globalization;
using StandardAbstraction;
using NUnit.Framework;
using NSubstitute;

namespace ApplicationMetrics.MetricLoggers.UnitTests
{
    /// <summary>
    /// Unit tests for class ApplicationMetrics.MetricLoggers.MetricLoggerBase.
    /// </summary>
    public class MetricLoggerBaseTests
    {
        private IStopwatch mockStopWatch;
        private IGuidProvider mockGuidProvider;
        private MetricLoggerBaseWithProtectedMembers testMetricLoggerBase;

        [SetUp]
        protected void SetUp()
        {
            mockStopWatch = Substitute.For<IStopwatch>();
            mockGuidProvider = Substitute.For<IGuidProvider>();
            mockStopWatch.Frequency.Returns<Int64>(10_000_000);
            testMetricLoggerBase = new MetricLoggerBaseWithProtectedMembers(IntervalMetricBaseTimeUnit.Millisecond, true, true, mockStopWatch, mockGuidProvider);
            testMetricLoggerBase.InterleavedIntervalMetricsMode = true;
        }

        // Note testing with 'supportConcurrency' parameter set false is not covered in this test class, as those code paths are covered in tests for class MetricLoggerBuffer.

        [Test]
        public void ProcessStartIntervalMetricEvent_NonInterleavedModeIntervalMetricCheckingTrueDuplicateIntervalMetrics()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var eventTime1 = GenerateUtcDateTime("2025-03-15 22:31:52.000");
            var eventTime2 = GenerateUtcDateTime("2025-03-15 22:31:52.005");
            testMetricLoggerBase.InterleavedIntervalMetricsMode = false;
            testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, intervalMetric, eventTime1);

            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, intervalMetric, eventTime2);
            });

            Assert.That(e.Message, Does.StartWith($"Received duplicate begin 'DiskReadTime' metrics."));
        }

        [Test]
        public void ProcessStartIntervalMetricEvent_NonInterleavedModeIntervalMetricCheckingFalseDuplicateIntervalMetrics()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var eventTime1 = GenerateUtcDateTime("2025-03-15 22:31:52.000");
            var eventTime2 = GenerateUtcDateTime("2025-03-15 22:31:52.005");
            var endEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.010");
            testMetricLoggerBase = new MetricLoggerBaseWithProtectedMembers(IntervalMetricBaseTimeUnit.Millisecond, false, true, mockStopWatch, mockGuidProvider);
            testMetricLoggerBase.InterleavedIntervalMetricsMode = false;
            testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, intervalMetric, eventTime1);

            testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, intervalMetric, eventTime2);
            Tuple<MetricLoggerBaseWithProtectedMembers.PublicIntervalMetricEventInstance, Int64> result = testMetricLoggerBase.ProcessEndIntervalMetricEvent(beginId, intervalMetric, endEventTime);

            Assert.AreEqual(MetricLoggerBaseWithProtectedMembers.PublicIntervalMetricEventTimePoint.Start, result.Item1.TimePoint);
            Assert.IsInstanceOf<DiskReadTime>(result.Item1.Metric);
            Assert.AreEqual(typeof(DiskReadTime), result.Item1.MetricType);
            Assert.AreEqual(eventTime2, result.Item1.EventTime);
            Assert.AreEqual(5, result.Item2);
        }

        [Test]
        public void ProcessEndIntervalMetricEvent_InterleavedModeEndIntervalMetricWithNoBegin()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var eventTime = GenerateUtcDateTime("2025-03-15 22:31:52.000");

            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBase.ProcessEndIntervalMetricEvent(beginId, intervalMetric, eventTime);
            });

            Assert.That(e.Message, Does.StartWith($"Received end 'DiskReadTime' with BeginId '92d8985d-6394-4d97-97bf-8aaf95c97214' with no corresponding begin interval metric."));
        }

        [Test]
        public void ProcessEndIntervalMetricEvent_InterleavedModeEndIntervalMetricTypeDoesntMatchBegin()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var beginEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.000");
            var endEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.010");
            testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, new DiskWriteTime(), beginEventTime);

            var e = Assert.Throws<ArgumentException>(delegate
            {
                testMetricLoggerBase.ProcessEndIntervalMetricEvent(beginId, intervalMetric, endEventTime);
            });

            Assert.That(e.Message, Does.StartWith($"Metric started with BeginId '92d8985d-6394-4d97-97bf-8aaf95c97214' was a 'DiskWriteTime' metric, but End() method was called with a 'DiskReadTime' metric."));
        }

        [Test]
        public void ProcessEndIntervalMetricEvent_InterleavedMode()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var beginEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.000");
            var endEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.010");
            testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, new DiskReadTime(), beginEventTime);

            Tuple<MetricLoggerBaseWithProtectedMembers.PublicIntervalMetricEventInstance, Int64> result = testMetricLoggerBase.ProcessEndIntervalMetricEvent(beginId, intervalMetric, endEventTime);

            Assert.AreEqual(MetricLoggerBaseWithProtectedMembers.PublicIntervalMetricEventTimePoint.Start, result.Item1.TimePoint);
            Assert.IsInstanceOf<DiskReadTime>(result.Item1.Metric);
            Assert.AreEqual(typeof(DiskReadTime), result.Item1.MetricType);
            Assert.AreEqual(beginEventTime, result.Item1.EventTime);
            Assert.AreEqual(10, result.Item2);
        }

        [Test]
        public void ProcessEndIntervalMetricEvent_InterleavedModeNegativeIntervalDuration()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var beginEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.000");
            var endEventTime = GenerateUtcDateTime("2025-03-15 22:31:51.010");
            testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, new DiskReadTime(), beginEventTime);

            Tuple<MetricLoggerBaseWithProtectedMembers.PublicIntervalMetricEventInstance, Int64> result = testMetricLoggerBase.ProcessEndIntervalMetricEvent(beginId, intervalMetric, endEventTime);

            Assert.AreEqual(MetricLoggerBaseWithProtectedMembers.PublicIntervalMetricEventTimePoint.Start, result.Item1.TimePoint);
            Assert.IsInstanceOf<DiskReadTime>(result.Item1.Metric);
            Assert.AreEqual(typeof(DiskReadTime), result.Item1.MetricType);
            Assert.AreEqual(beginEventTime, result.Item1.EventTime);
            Assert.AreEqual(0, result.Item2);
        }

        [Test]
        public void ProcessEndIntervalMetricEvent_InterleavedModeNanosecondIntervalDuration()
        {
            testMetricLoggerBase = new MetricLoggerBaseWithProtectedMembers(IntervalMetricBaseTimeUnit.Nanosecond, true, true, mockStopWatch, mockGuidProvider);
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var beginEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.000");
            var endEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.010");
            testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, new DiskReadTime(), beginEventTime);

            Tuple<MetricLoggerBaseWithProtectedMembers.PublicIntervalMetricEventInstance, Int64> result = testMetricLoggerBase.ProcessEndIntervalMetricEvent(beginId, intervalMetric, endEventTime);

            Assert.AreEqual(MetricLoggerBaseWithProtectedMembers.PublicIntervalMetricEventTimePoint.Start, result.Item1.TimePoint);
            Assert.IsInstanceOf<DiskReadTime>(result.Item1.Metric);
            Assert.AreEqual(typeof(DiskReadTime), result.Item1.MetricType);
            Assert.AreEqual(beginEventTime, result.Item1.EventTime);
            Assert.AreEqual(10_000_000, result.Item2);
        }

        [Test]
        public void ProcessEndIntervalMetricEvent_NonInterleavedModeIntervalMetricCheckingTrueEndIntervalMetricWithNoBegin()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var endEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.010");
            testMetricLoggerBase.InterleavedIntervalMetricsMode = false;

            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBase.ProcessEndIntervalMetricEvent(beginId, intervalMetric, endEventTime);
            });

            Assert.That(e.Message, Does.StartWith($"Received end 'DiskReadTime' with no corresponding begin interval metric."));
        }

        [Test]
        public void ProcessEndIntervalMetricEvent_NonInterleavedModeIntervalMetricCheckingFalseEndIntervalMetricWithNoBegin()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var endEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.010");
            testMetricLoggerBase = new MetricLoggerBaseWithProtectedMembers(IntervalMetricBaseTimeUnit.Nanosecond, false, true, mockStopWatch, mockGuidProvider);
            testMetricLoggerBase.InterleavedIntervalMetricsMode = false;

            Tuple<MetricLoggerBaseWithProtectedMembers.PublicIntervalMetricEventInstance, Int64> result = testMetricLoggerBase.ProcessEndIntervalMetricEvent(beginId, intervalMetric, endEventTime);

            Assert.IsNull(result);
        }

        [Test]
        public void ProcessEndIntervalMetricEvent_NonInterleavedMode()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var beginEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.000");
            var endEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.010");
            testMetricLoggerBase.InterleavedIntervalMetricsMode = false;
            testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, new DiskReadTime(), beginEventTime);

            Tuple<MetricLoggerBaseWithProtectedMembers.PublicIntervalMetricEventInstance, Int64> result = testMetricLoggerBase.ProcessEndIntervalMetricEvent(beginId, intervalMetric, endEventTime);

            Assert.AreEqual(MetricLoggerBaseWithProtectedMembers.PublicIntervalMetricEventTimePoint.Start, result.Item1.TimePoint);
            Assert.IsInstanceOf<DiskReadTime>(result.Item1.Metric);
            Assert.AreEqual(typeof(DiskReadTime), result.Item1.MetricType);
            Assert.AreEqual(beginEventTime, result.Item1.EventTime);
            Assert.AreEqual(10, result.Item2);
        }

        [Test]
        public void ProcessEndIntervalMetricEvent_NonInterleavedModeNegativeIntervalDuration()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var beginEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.000");
            var endEventTime = GenerateUtcDateTime("2025-03-15 22:31:51.010");
            testMetricLoggerBase.InterleavedIntervalMetricsMode = false;
            testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, new DiskReadTime(), beginEventTime);

            Tuple<MetricLoggerBaseWithProtectedMembers.PublicIntervalMetricEventInstance, Int64> result = testMetricLoggerBase.ProcessEndIntervalMetricEvent(beginId, intervalMetric, endEventTime);

            Assert.AreEqual(MetricLoggerBaseWithProtectedMembers.PublicIntervalMetricEventTimePoint.Start, result.Item1.TimePoint);
            Assert.IsInstanceOf<DiskReadTime>(result.Item1.Metric);
            Assert.AreEqual(typeof(DiskReadTime), result.Item1.MetricType);
            Assert.AreEqual(beginEventTime, result.Item1.EventTime);
            Assert.AreEqual(0, result.Item2);
        }

        [Test]
        public void ProcessEndIntervalMetricEvent_NonInterleavedModeNanosecondIntervalDuration()
        {
            testMetricLoggerBase = new MetricLoggerBaseWithProtectedMembers(IntervalMetricBaseTimeUnit.Nanosecond, true, true, mockStopWatch, mockGuidProvider);
            testMetricLoggerBase.InterleavedIntervalMetricsMode = false;
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var beginEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.000");
            var endEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.010");
            testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, new DiskReadTime(), beginEventTime);

            Tuple<MetricLoggerBaseWithProtectedMembers.PublicIntervalMetricEventInstance, Int64> result = testMetricLoggerBase.ProcessEndIntervalMetricEvent(beginId, intervalMetric, endEventTime);

            Assert.AreEqual(MetricLoggerBaseWithProtectedMembers.PublicIntervalMetricEventTimePoint.Start, result.Item1.TimePoint);
            Assert.IsInstanceOf<DiskReadTime>(result.Item1.Metric);
            Assert.AreEqual(typeof(DiskReadTime), result.Item1.MetricType);
            Assert.AreEqual(beginEventTime, result.Item1.EventTime);
            Assert.AreEqual(10_000_000, result.Item2);
        }

        [Test]
        public void ProcessCancelIntervalMetricEvent_InterleavedModeEndIntervalMetricWithNoBegin()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var eventTime = GenerateUtcDateTime("2025-03-15 22:31:52.000");

            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBase.ProcessCancelIntervalMetricEvent(beginId, intervalMetric, eventTime);
            });

            Assert.That(e.Message, Does.StartWith($"Received cancel 'DiskReadTime' with BeginId '92d8985d-6394-4d97-97bf-8aaf95c97214' with no corresponding begin interval metric."));
        }

        [Test]
        public void ProcessCancelIntervalMetricEvent_InterleavedModeEndIntervalMetricTypeDoesntMatchBegin()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var beginEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.000");
            var cancelEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.010");
            testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, new DiskWriteTime(), beginEventTime);

            var e = Assert.Throws<ArgumentException>(delegate
            {
                testMetricLoggerBase.ProcessCancelIntervalMetricEvent(beginId, intervalMetric, cancelEventTime);
            });

            Assert.That(e.Message, Does.StartWith($"Metric started with BeginId '92d8985d-6394-4d97-97bf-8aaf95c97214' was a 'DiskWriteTime' metric, but CancelBegin() method was called with a 'DiskReadTime' metric."));
        }

        [Test]
        public void ProcessCancelIntervalMetricEvent_InterleavedMode()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var beginEventTime1 = GenerateUtcDateTime("2025-03-15 22:31:52.000");
            var cancelEventTime1 = GenerateUtcDateTime("2025-03-15 22:31:52.010");
            var beginEventTime2 = GenerateUtcDateTime("2025-03-16 09:35:53.000");
            var cancelEventTime2 = GenerateUtcDateTime("2025-03-16 09:35:53.010");
            testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, intervalMetric, beginEventTime1);
            testMetricLoggerBase.ProcessCancelIntervalMetricEvent(beginId, intervalMetric, cancelEventTime1);
            testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, intervalMetric, beginEventTime2);

            testMetricLoggerBase.ProcessCancelIntervalMetricEvent(beginId, intervalMetric, cancelEventTime2);
        }

        [Test]
        public void ProcessCancelIntervalMetricEvent_NonInterleavedModeIntervalMetricCheckingTrueEndIntervalMetricWithNoBegin()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var cancelEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.010");
            testMetricLoggerBase.InterleavedIntervalMetricsMode = false;

            var e = Assert.Throws<InvalidOperationException>(delegate
            {
                testMetricLoggerBase.ProcessCancelIntervalMetricEvent(beginId, intervalMetric, cancelEventTime);
            });

            Assert.That(e.Message, Does.StartWith($"Received cancel 'DiskReadTime' with no corresponding begin interval metric."));
        }

        [Test]
        public void ProcessCancelIntervalMetricEvent_NonInterleavedModeIntervalMetricCheckingFalseEndIntervalMetricWithNoBegin()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var endEventTime = GenerateUtcDateTime("2025-03-15 22:31:52.010");
            testMetricLoggerBase = new MetricLoggerBaseWithProtectedMembers(IntervalMetricBaseTimeUnit.Nanosecond, false, true, mockStopWatch, mockGuidProvider);
            testMetricLoggerBase.InterleavedIntervalMetricsMode = false;

            testMetricLoggerBase.ProcessCancelIntervalMetricEvent(beginId, intervalMetric, endEventTime);
        }

        [Test]
        public void ProcessCancelIntervalMetricEvent_NonInterleavedModeIntervalMetricCheckingFalse()
        {
            var beginId = Guid.Parse("92d8985d-6394-4d97-97bf-8aaf95c97214");
            var intervalMetric = new DiskReadTime();
            var beginEventTime1 = GenerateUtcDateTime("2025-03-15 22:31:52.000");
            var cancelEventTime1 = GenerateUtcDateTime("2025-03-15 22:31:52.010");
            var beginEventTime2 = GenerateUtcDateTime("2025-03-16 09:35:53.000");
            var cancelEventTime2 = GenerateUtcDateTime("2025-03-16 09:35:53.010");
            testMetricLoggerBase = new MetricLoggerBaseWithProtectedMembers(IntervalMetricBaseTimeUnit.Nanosecond, false, true, mockStopWatch, mockGuidProvider);
            testMetricLoggerBase.InterleavedIntervalMetricsMode = false;
            testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, intervalMetric, beginEventTime1);
            testMetricLoggerBase.ProcessCancelIntervalMetricEvent(beginId, intervalMetric, cancelEventTime1);
            testMetricLoggerBase.ProcessStartIntervalMetricEvent(beginId, intervalMetric, beginEventTime2);

            testMetricLoggerBase.ProcessCancelIntervalMetricEvent(beginId, intervalMetric, cancelEventTime2);
        }

        #region Private/Protected Methods

        /// <summary>
        /// Generates as UTC <see cref="System.DateTime"/> from the specified string containing a date in ISO format.
        /// </summary>
        /// <param name="isoFormattedDateString">The date string.</param>
        /// <returns>the DateTime.</returns>
        private System.DateTime GenerateUtcDateTime(String isoFormattedDateString)
        {
            var returnDateTime = System.DateTime.ParseExact(isoFormattedDateString, "yyyy-MM-dd HH:mm:ss.fff", DateTimeFormatInfo.InvariantInfo);
            return System.DateTime.SpecifyKind(returnDateTime, DateTimeKind.Utc);
        }

        #endregion

        #region Inner Classes

        /// <summary>
        /// Non-abstract version of the MetricLoggerBase class where protected members are exposed as public so that they can be unit tested.
        /// </summary>
        private class MetricLoggerBaseWithProtectedMembers : MetricLoggerBase
        {
            /// <summary>Whether to support interleaving when processing interval metrics.</summary>
            public Nullable<Boolean> InterleavedIntervalMetricsMode
            {
                set { interleavedIntervalMetricsMode = value; }
            }

            /// <summary>
            /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBase class.
            /// </summary>
            /// <param name="intervalMetricBaseTimeUnit">The base time unit to use to log interval metrics.</param>
            /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).  Note that this parameter only has an effect when running in 'non-interleaved' mode.</param>
            /// <param name="supportConcurrency">Whether the methods in the class will be called concurrently from multiple threads.</param>
            /// <param name="stopWatch">Used to measure elapsed time since starting the buffer processor.</param>
            /// <param name="guidProvider">Object which provides Guids.</param>
            public MetricLoggerBaseWithProtectedMembers(IntervalMetricBaseTimeUnit intervalMetricBaseTimeUnit, Boolean intervalMetricChecking, Boolean supportConcurrency, IStopwatch stopWatch, IGuidProvider guidProvider)
                : base(intervalMetricBaseTimeUnit, intervalMetricChecking, supportConcurrency, stopWatch, guidProvider)
            {
            }

            /// <summary>
            /// Processes a UniqueIntervalMetricEventInstance with a 'Start' TimePoint.
            /// </summary>
            /// <param name="beginId">A unique id representing the starting of the interval metric event (should match the <see cref="Guid"/> returned from the <see cref="IMetricLogger.Begin(IntervalMetric)"/> method).</param>
            /// <param name="intervalMetric">The metric which occurred.</param>
            /// <param name="eventTime">The date and time the metric event started, expressed as UTC.</param>
            public new void ProcessStartIntervalMetricEvent(Guid beginId, IntervalMetric intervalMetric, System.DateTime eventTime)
            {
                base.ProcessStartIntervalMetricEvent(new UniqueIntervalMetricEventInstance(beginId, intervalMetric, IntervalMetricEventTimePoint.Start, eventTime));
            }

            /// <summary>
            /// Processes a UniqueIntervalMetricEventInstance with a 'End' TimePoint.
            /// </summary>
            /// <param name="beginId">A unique id representing the starting of the interval metric event (should match the <see cref="Guid"/> returned from the <see cref="IMetricLogger.Begin(IntervalMetric)"/> method).</param>
            /// <param name="intervalMetric">The metric which occurred.</param>
            /// <param name="eventTime">The date and time the metric event started, expressed as UTC.</param>
            /// <returns>A tuple containing 2 values: the IntervalMetricEventInstance generated as a result of the metric end, and the duration of the interval metric.  Returns null if no metric was generated (e.g. in the case constructor parameter 'intervalMetricChecking' was set to false).</returns>
            public new Tuple<PublicIntervalMetricEventInstance, Int64> ProcessEndIntervalMetricEvent(Guid beginId, IntervalMetric intervalMetric, System.DateTime eventTime)
            {
                Tuple<IntervalMetricEventInstance, Int64> result = base.ProcessEndIntervalMetricEvent(new UniqueIntervalMetricEventInstance(beginId, intervalMetric, IntervalMetricEventTimePoint.End, eventTime));
                if (result == null)
                {
                    return null;
                }
                else
                {
                    var returnIntervalMetricEventInstance = new PublicIntervalMetricEventInstance(result.Item1.TimePoint.ToString(), result.Item1.Metric, result.Item1.EventTime);
                    return Tuple.Create(returnIntervalMetricEventInstance, result.Item2);
                }
            }

            /// <summary>
            /// Processes a UniqueIntervalMetricEventInstance with a 'Cancel' TimePoint.
            /// </summary>
            /// <param name="beginId">A unique id representing the starting of the interval metric event (should match the <see cref="Guid"/> returned from the <see cref="IMetricLogger.Begin(IntervalMetric)"/> method).</param>
            /// <param name="intervalMetric">The metric which occurred.</param>
            /// <param name="eventTime">The date and time the metric event started, expressed as UTC.</param>
            public new void ProcessCancelIntervalMetricEvent(Guid beginId, IntervalMetric intervalMetric, System.DateTime eventTime)
            {
                base.ProcessCancelIntervalMetricEvent(new UniqueIntervalMetricEventInstance(beginId, intervalMetric, IntervalMetricEventTimePoint.Cancel, eventTime));
            }

            /// <summary>
            /// A clone of the MetricLoggerBase+IntervalMetricEventTimePoint enum which is accessible outside MetricLoggerBase.
            /// </summary>
            public enum PublicIntervalMetricEventTimePoint
            {
                /// <summary>The start of the interval metric event.</summary>
                Start,
                /// <summary>The completion of the interval metric event.</summary>
                End,
                /// <summary>The cancellation of a previously started interval metric event.</summary>
                Cancel
            }

            /// <summary>
            /// A clone of the MetricLoggerBase+IntervalMetricEventInstance class which is accessible outside MetricLoggerBase.
            /// </summary>
            public class PublicIntervalMetricEventInstance
            {
                /// <summary>Whether the event represents the start or the end of the interval metric.</summary>
                public PublicIntervalMetricEventTimePoint TimePoint { get;  protected set; }

                /// <summary>The interval metric that occurred.</summary>
                public IntervalMetric Metric { get; protected set; }

                /// <summary>Returns the type of the metric that occurred.</summary>
                public Type MetricType
                {
                    get
                    {
                        return Metric.GetType();
                    }
                }

                /// <summary>
                /// The date and time the event occurred, expressed as UTC.
                /// </summary>
                public System.DateTime EventTime { get; protected set; }

                /// <summary>
                /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.MetricLoggerBase+MetricLoggerBaseWithProtectedMembers+PublicIntervalMetricEventInstance class.
                /// </summary>
                /// <param name="timePointAsString">A stringified IntervalMetricEventTimePoint representing whether the event represents the start or the end of the interval metric..</param>
                /// <param name="metric">The interval metric that occurred.</param>
                /// <param name="eventTime">The date and time the event occurred, expressed as UTC.</param>
                public PublicIntervalMetricEventInstance(String timePointAsString, IntervalMetric metric, System.DateTime eventTime)
                {
                    TimePoint = (PublicIntervalMetricEventTimePoint)Enum.Parse(typeof(PublicIntervalMetricEventTimePoint), timePointAsString);
                    Metric = metric;
                    EventTime = eventTime;
                }
            }
        }

        #endregion
    }
}
