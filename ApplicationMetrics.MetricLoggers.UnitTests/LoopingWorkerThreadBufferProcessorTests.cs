/*
 * Copyright 2021 Alastair Wyse (https://github.com/alastairwyse/ApplicationMetrics/)
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

namespace ApplicationMetrics.MetricLoggers.UnitTests
{
    /// <summary>
    /// Unit tests for class ApplicationMetrics.MetricLoggers.LoopingWorkerThreadBufferProcessor.
    /// </summary>
    public class LoopingWorkerThreadBufferProcessorTests
    {
        private LoopingWorkerThreadBufferProcessor testLoopingWorkerThreadBufferProcessor;

        [SetUp]
        protected void SetUp()
        {
            testLoopingWorkerThreadBufferProcessor = new LoopingWorkerThreadBufferProcessor(500);
        }

        [TearDown]
        protected void TearDown()
        {
            testLoopingWorkerThreadBufferProcessor.Dispose();
        }

        [Test]
        public void Constructor_DequeueOperationLoopIntervalParameterInvalid()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(delegate
            {
                testLoopingWorkerThreadBufferProcessor = new LoopingWorkerThreadBufferProcessor(0);
            });

            Assert.That(e.Message, Does.StartWith("Argument 'dequeueOperationLoopInterval' must be greater than or equal to 1."));
            Assert.AreEqual(e.ParamName, "dequeueOperationLoopInterval");
        }

        [Test]
        public void BufferProcessedEventRaisedAfterStop()
        {
            // Tests that the BufferProcessed event is raised if the buffers still contain metric events after the Stop() method is called.
            //   This unit test is not deterministic, and assumes that the operating system will schedule the main and worker threads so that the calls to NotifyCountMetricEventBuffered() and Stop() will occur before the worker thread has completed one iteration of its loop.

            int bufferProcessedEventRaisedCount = 0;
            testLoopingWorkerThreadBufferProcessor.BufferProcessed += delegate(object sender, EventArgs e) { bufferProcessedEventRaisedCount++; };

            testLoopingWorkerThreadBufferProcessor.Start();
            Thread.Sleep(250);
            testLoopingWorkerThreadBufferProcessor.NotifyCountMetricEventBuffered();
            testLoopingWorkerThreadBufferProcessor.Stop();

            Assert.AreEqual(2, bufferProcessedEventRaisedCount);
        }
    }
}
