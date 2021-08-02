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

#pragma warning disable 1591

using System;
using System.Threading;
using NUnit.Framework;
using NSubstitute;
using StandardAbstraction;

namespace ApplicationMetrics.MetricLoggers.UnitTests
{
    /// <summary>
    /// Unit tests for class ApplicationMetrics.MetricLoggers.FileMetricLogger.
    /// </summary>
    [TestFixture]
    class FileMetricLoggerTests
    {
        /* 
         * NOTE: As most of the work of the FileMetricLogger class is done by a worker thread, many of the tests in this class rely on checking the behavior of the worker thread.
         *       This creates an issue in the unit test code as NUnit does not catch exceptions thrown on the worker thread via the Assert.Throws() method.
         *         To work around this an ExceptionStorer object is injected into the test class.  Any exceptions thrown are checked in this object rather than via the Assert.Throws() method.
         */

        private IStreamWriter mockStreamWriter;
        private IDateTime mockDateTime;
        private IStopwatch mockStopWatch;
        private ExceptionStorer exceptionStorer;
        private FileMetricLogger testFileMetricLogger;
        private const string dateTimeFormat = "yyyy-MM-dd HH:mm:ss.fff";

        [SetUp]
        protected void SetUp()
        {
            mockStreamWriter = Substitute.For<IStreamWriter>();
            mockDateTime = Substitute.For<IDateTime>();
            mockStopWatch = Substitute.For<IStopwatch>();
            exceptionStorer = new ExceptionStorer();
            testFileMetricLogger = new FileMetricLogger('|', new LoopingWorkerThreadBufferProcessor(10), true, mockStreamWriter, mockDateTime, mockStopWatch, exceptionStorer);
        }

        [Test]
        public void IncrementSuccessTest()
        {
            TimeSpan utcOffset = TimeZone.CurrentTimeZone.GetUtcOffset(System.DateTime.Now);
            System.DateTime timeStamp1 = new System.DateTime(2014, 6, 14, 12, 45, 31);
            System.DateTime timeStamp2 = new System.DateTime(2014, 6, 14, 12, 45, 43);
            System.DateTime timeStamp3 = new System.DateTime(2014, 6, 14, 12, 45, 47);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 6, 14, 12, 45, 30, DateTimeKind.Utc)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Increment()
                10000000L,
                130000000L,
                170000000L
            );

            testFileMetricLogger.Start();
            testFileMetricLogger.Increment(new TestMessageReceivedMetric());
            testFileMetricLogger.Increment(new TestDiskReadOperationMetric());
            testFileMetricLogger.Increment(new TestMessageReceivedMetric());
            testFileMetricLogger.Stop();
            // TODO: This is obviously not a good way to ensure the code under test completes, but best I can do for the moment without reasonable changes to the classes
            //   However, need to improve this at some point
            Thread.Sleep(100);

            Assert.IsNull(exceptionStorer.StoredException);
            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(3).ElapsedTicks;
            mockStreamWriter.Received(1).WriteLine(timeStamp1.Add(utcOffset).ToString(dateTimeFormat) + " | " + new TestMessageReceivedMetric().Name);
            mockStreamWriter.Received(1).WriteLine(timeStamp2.Add(utcOffset).ToString(dateTimeFormat) + " | " + new TestDiskReadOperationMetric().Name);
            mockStreamWriter.Received(1).WriteLine(timeStamp3.Add(utcOffset).ToString(dateTimeFormat) + " | " + new TestMessageReceivedMetric().Name);
            mockStreamWriter.Received(3).Flush();
        }

        [Test]
        public void AddSuccessTest()
        {
            TimeSpan utcOffset = TimeZone.CurrentTimeZone.GetUtcOffset(System.DateTime.Now);
            System.DateTime timeStamp1 = new System.DateTime(2014, 6, 14, 12, 45, 31);
            System.DateTime timeStamp2 = new System.DateTime(2014, 6, 14, 12, 45, 43);
            System.DateTime timeStamp3 = new System.DateTime(2014, 6, 14, 12, 45, 47);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 6, 14, 12, 45, 30, DateTimeKind.Utc)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Add()
                10000000L,
                130000000L,
                170000000L
            );

            testFileMetricLogger.Start();
            testFileMetricLogger.Add(new TestMessageBytesReceivedMetric(12345));
            testFileMetricLogger.Add(new TestDiskBytesReadMetric(160307));
            testFileMetricLogger.Add(new TestMessageBytesReceivedMetric(12347));
            testFileMetricLogger.Stop();
            Thread.Sleep(100);

            Assert.IsNull(exceptionStorer.StoredException);
            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(3).ElapsedTicks;
            mockStreamWriter.Received(1).WriteLine(timeStamp1.Add(utcOffset).ToString(dateTimeFormat) + " | " + new TestMessageBytesReceivedMetric(0).Name + " | " + 12345);
            mockStreamWriter.Received(1).WriteLine(timeStamp2.Add(utcOffset).ToString(dateTimeFormat) + " | " + new TestDiskBytesReadMetric(0).Name + " | " + 160307);
            mockStreamWriter.Received(1).WriteLine(timeStamp3.Add(utcOffset).ToString(dateTimeFormat) + " | " + new TestMessageBytesReceivedMetric(0).Name + " | " + 12347);
            mockStreamWriter.Received(3).Flush();
        }

        [Test]
        public void SetSuccessTest()
        {
            TimeSpan utcOffset = TimeZone.CurrentTimeZone.GetUtcOffset(System.DateTime.Now);
            System.DateTime timeStamp1 = new System.DateTime(2014, 6, 17, 23, 42, 33);
            System.DateTime timeStamp2 = new System.DateTime(2014, 6, 17, 23, 44, 35);
            System.DateTime timeStamp3 = new System.DateTime(2014, 6, 17, 23, 59, 01);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                new System.DateTime(2014, 6, 17, 23, 42, 0, DateTimeKind.Utc)
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Set()
                330000000L, 
                1550000000L, 
                10210000000L
            );

            testFileMetricLogger.Start();
            testFileMetricLogger.Set(new TestAvailableMemoryMetric(301156000));
            testFileMetricLogger.Set(new TestFreeWorkerThreadsMetric(12));
            testFileMetricLogger.Set(new TestAvailableMemoryMetric(301155987));
            testFileMetricLogger.Stop();
            Thread.Sleep(100);

            Assert.IsNull(exceptionStorer.StoredException);
            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(3).ElapsedTicks;
            mockStreamWriter.Received(1).WriteLine(timeStamp1.Add(utcOffset).ToString(dateTimeFormat) + " | " + new TestAvailableMemoryMetric(0).Name + " | " + 301156000);
            mockStreamWriter.Received(1).WriteLine(timeStamp2.Add(utcOffset).ToString(dateTimeFormat) + " | " + new TestFreeWorkerThreadsMetric(0).Name + " | " + 12);
            mockStreamWriter.Received(1).WriteLine(timeStamp3.Add(utcOffset).ToString(dateTimeFormat) + " | " + new TestAvailableMemoryMetric(0).Name + " | " + 301155987);
            mockStreamWriter.Received(3).Flush();
        }

        [Test]
        public void BeginEndSuccessTests()
        {
            TimeSpan utcOffset = TimeZone.CurrentTimeZone.GetUtcOffset(System.DateTime.Now);
            System.DateTime timeStamp1 = new System.DateTime(2014, 6, 14, 12, 45, 31, 000);
            System.DateTime timeStamp2 = new System.DateTime(2014, 6, 14, 12, 45, 31, 034);
            System.DateTime timeStamp3 = new System.DateTime(2014, 6, 14, 12, 45, 43, 500);
            System.DateTime timeStamp4 = new System.DateTime(2014, 6, 14, 12, 45, 43, 499);
            System.DateTime timeStamp5 = new System.DateTime(2014, 6, 15, 23, 58, 47, 750);
            System.DateTime timeStamp6 = new System.DateTime(2014, 6, 15, 23, 58, 48, 785);
            mockDateTime.UtcNow.Returns<System.DateTime>
            (
                // Returns for calls to Start()
                timeStamp1
            );
            mockStopWatch.ElapsedTicks.Returns<Int64>
            (
                // Returns for calls to Begin() / End()
                0L, 
                340000L,
                125000000L,
                // Note below expect makes the end time before the begin time.  Class should insert the resulting milliseconds interval as 0.
                124990000L,
                1267967500000L,
                1267977850000L
            );

            testFileMetricLogger.Start();
            testFileMetricLogger.Begin(new TestDiskReadTimeMetric());
            testFileMetricLogger.End(new TestDiskReadTimeMetric());
            testFileMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testFileMetricLogger.End(new TestMessageProcessingTimeMetric());
            testFileMetricLogger.Begin(new TestMessageProcessingTimeMetric());
            testFileMetricLogger.End(new TestMessageProcessingTimeMetric());
            testFileMetricLogger.Stop();
            Thread.Sleep(100);

            Assert.IsNull(exceptionStorer.StoredException);
            var throwAway1 = mockDateTime.Received(1).UtcNow;
            var throwAway2 = mockStopWatch.Received(6).ElapsedTicks;
            mockStreamWriter.Received(1).WriteLine(timeStamp1.Add(utcOffset).ToString(dateTimeFormat) + " | " + new TestDiskReadTimeMetric().Name + " | " + 34);
            mockStreamWriter.Received(1).WriteLine(timeStamp3.Add(utcOffset).ToString(dateTimeFormat) + " | " + new TestMessageProcessingTimeMetric().Name + " | " + 0);
            mockStreamWriter.Received(1).WriteLine(timeStamp5.Add(utcOffset).ToString(dateTimeFormat) + " | " + new TestMessageProcessingTimeMetric().Name + " | " + 1035);
            mockStreamWriter.Received(3).Flush();
        }

        [Test]
        public void CloseSuccessTest()
        {
            testFileMetricLogger.Close();

            Assert.IsNull(exceptionStorer.StoredException);
            mockStreamWriter.Received(1).Close();
        }

        [Test]
        public void DisposeSuccessTest()
        {
            testFileMetricLogger.Dispose();

            Assert.IsNull(exceptionStorer.StoredException);
            mockStreamWriter.Received(1).Dispose();
        }
    }
}
