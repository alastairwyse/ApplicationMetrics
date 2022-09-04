/*
 * Copyright 2017 Alastair Wyse (https://github.com/alastairwyse/ApplicationMetrics/)
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
using System.Text;
using System.Collections.Generic;
using StandardAbstraction;

namespace ApplicationMetrics.MetricLoggers
{
    /// <summary>
    /// Writes metric and instrumentation events for an application to a file.
    /// </summary>
    /// <remarks>Since version 5.0.0, the class supports an 'interleaved' mode for interval metrics.  This allows multiple interval metrics of the same type to be started concurrently (e.g. to support scenarios where client code is running on multiple concurrent threads).  The <see cref="IMetricLogger.Begin(IntervalMetric)"/>, <see cref="IMetricLogger.End(Guid, IntervalMetric)"/>, and <see cref="IMetricLogger.CancelBegin(Guid, IntervalMetric)"/> methods support an optional <see cref="Guid"/> return value and parameter which is used to associate/link calls to those methods.  For backwards compatability the non-<see cref="Guid"/> versions of these methods are maintained.  The mode (interleaved or non-interleaved) is chosen on the first call to either the <see cref="IMetricLogger.End(Guid, IntervalMetric)"/> or <see cref="IMetricLogger.CancelBegin(Guid, IntervalMetric)"/> methods, selecting interleaved mode if the <see cref="Guid"/> overload versions of the methods are called, and non-interleaved if the non-<see cref="Guid"/> overload versions are called.  Once the mode is set, calling method overloads corresponding to the other mode will throw an exception, so client code must consistently call either the <see cref="Guid"/> or non-<see cref="Guid"/> overloads of these methods.  Non-interleaved mode may be deprecated in future versions, so it is recommended to migrate client code to support interleaved mode.</remarks>
    public class FileMetricLogger : MetricLoggerBuffer, IDisposable
    {
        private const string dateTimeFormat = "yyyy-MM-dd HH:mm:ss.fff";
        private char separatorCharacter;
        private IStreamWriter streamWriter;
        private Encoding fileEncoding = Encoding.UTF8;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.FileMetricLogger class.
        /// </summary>
        /// <param name="separatorCharacter">The character to use to separate fields (e.g. date/time stamp, metric name) in the file.</param>
        /// <param name="filePath">The full path of the file to write the metric events to.</param>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).</param>
        public FileMetricLogger(char separatorCharacter, string filePath, IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking)
            : base(bufferProcessingStrategy, intervalMetricChecking)
        {
            this.separatorCharacter = separatorCharacter;
            streamWriter = new StreamWriter(filePath, false, fileEncoding);
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.FileMetricLoggerImplementation class.
        /// </summary>
        /// <param name="separatorCharacter">The character to use to separate fields (e.g. date/time stamp, metric name) in the file.</param>
        /// <param name="filePath">The full path of the file to write the metric events to.</param>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).</param>
        /// <param name="appendToFile">Whether to append to an existing file (if it exists) or overwrite.  A value of true causes appending.</param>
        /// <param name="fileEncoding">The character encoding to use in the file.</param>
        public FileMetricLogger(char separatorCharacter, string filePath, IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking, bool appendToFile, Encoding fileEncoding)
            : base(bufferProcessingStrategy, intervalMetricChecking)
        {
            this.separatorCharacter = separatorCharacter;
            streamWriter = new StreamWriter(filePath, appendToFile, fileEncoding);
        }

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.FileMetricLoggerImplementation class.  Note this is an additional constructor to facilitate unit tests, and should not be used to instantiate the class under normal conditions.
        /// </summary>
        /// <param name="separatorCharacter">The character to use to separate fields (e.g. date/time stamp, metric name) in the file.</param>
        /// <param name="bufferProcessingStrategy">Object which implements a processing strategy for the buffers (queues).</param>
        /// <param name="intervalMetricChecking">Specifies whether an exception should be thrown if the correct order of interval metric logging is not followed (e.g. End() method called before Begin()).</param>
        /// <param name="streamWriter">A test (mock) stream writer.</param>
        /// <param name="dateTime">A test (mock) <see cref="IDateTime"/> object.</param>
        /// <param name="stopWatch">A test (mock) <see cref="IStopwatch"/> object.</param>
        /// <param name="guidProvider">A test (mock) <see cref="IGuidProvider"/> object.</param>
        public FileMetricLogger(char separatorCharacter, IBufferProcessingStrategy bufferProcessingStrategy, bool intervalMetricChecking, IStreamWriter streamWriter, IDateTime dateTime, IStopwatch stopWatch, IGuidProvider guidProvider)
            : base(bufferProcessingStrategy, intervalMetricChecking, dateTime, stopWatch, guidProvider)
        {
            this.separatorCharacter = separatorCharacter;
            this.streamWriter = streamWriter;
        }

        /// <summary>
        /// Closes the metric log file.
        /// </summary>
        public void Close()
        {
            streamWriter.Close();
        }

        #region Base Class Method Implementations

        /// <summary>
        /// Writes logged count metric events to the file.
        /// </summary>
        /// <param name="countMetricEvents">The count metric events to write.</param>
        protected override void ProcessCountMetricEvents(Queue<CountMetricEventInstance> countMetricEvents)
        {
            while (countMetricEvents.Count > 0)
            {
                CountMetricEventInstance currentCountMetricEventInstance = countMetricEvents.Dequeue();
                StringBuilder stringBuilder = InitializeStringBuilder(currentCountMetricEventInstance.EventTime.ToLocalTime());
                stringBuilder.Append(currentCountMetricEventInstance.Metric.Name);
                streamWriter.WriteLine(stringBuilder.ToString());
                streamWriter.Flush();
            }
        }

        /// <summary>
        /// Writes logged amount metric events to the file.
        /// </summary>
        /// <param name="amountMetricEvents">The amount metric events to write.</param>
        protected override void ProcessAmountMetricEvents(Queue<AmountMetricEventInstance> amountMetricEvents)
        {
            while (amountMetricEvents.Count > 0)
            {
                AmountMetricEventInstance currentAmountMetricEventInstance = amountMetricEvents.Dequeue();
                StringBuilder stringBuilder = InitializeStringBuilder(currentAmountMetricEventInstance.EventTime.ToLocalTime());
                stringBuilder.Append(currentAmountMetricEventInstance.Metric.Name);
                AppendSeparatorCharacter(stringBuilder);
                stringBuilder.Append(currentAmountMetricEventInstance.Amount);
                streamWriter.WriteLine(stringBuilder.ToString());
                streamWriter.Flush();
            }
        }

        /// <summary>
        /// Writes logged status metric events to the file.
        /// </summary>
        /// <param name="statusMetricEvents">The status metric events to write.</param>
        protected override void ProcessStatusMetricEvents(Queue<StatusMetricEventInstance> statusMetricEvents)
        {
            while (statusMetricEvents.Count > 0)
            {
                StatusMetricEventInstance currentStatustMetricEventInstance = statusMetricEvents.Dequeue();
                StringBuilder stringBuilder = InitializeStringBuilder(currentStatustMetricEventInstance.EventTime.ToLocalTime());
                stringBuilder.Append(currentStatustMetricEventInstance.Metric.Name);
                AppendSeparatorCharacter(stringBuilder);
                stringBuilder.Append(currentStatustMetricEventInstance.Value);
                streamWriter.WriteLine(stringBuilder.ToString());
                streamWriter.Flush();
            }
        }

        /// <summary>
        /// Writes interval count metric events to the file.
        /// </summary>
        /// <param name="intervalMetricEventsAndDurations">The interval metric events to write.</param>
        protected override void ProcessIntervalMetricEvents(Queue<Tuple<IntervalMetricEventInstance, Int64>> intervalMetricEventsAndDurations)
        {
            while (intervalMetricEventsAndDurations.Count > 0)
            {
                Tuple<IntervalMetricEventInstance, Int64> currentIntervalMetricEventAndDuration = intervalMetricEventsAndDurations.Dequeue();
                IntervalMetricEventInstance currentIntervalMetricEvent = currentIntervalMetricEventAndDuration.Item1;
                Int64 currentDuration = currentIntervalMetricEventAndDuration.Item2;
                StringBuilder stringBuilder = InitializeStringBuilder(currentIntervalMetricEvent.EventTime.ToLocalTime());
                stringBuilder.Append(currentIntervalMetricEvent.Metric.Name);
                AppendSeparatorCharacter(stringBuilder);
                stringBuilder.Append(currentDuration);
                streamWriter.WriteLine(stringBuilder.ToString());
                streamWriter.Flush();
            }
        }

        #endregion

        #region Private/Protected Methods

        /// <summary>
        /// Creates and returns a StringBuilder class, with the specified timestamp written to it.
        /// </summary>
        /// <param name="timeStamp">The timestamp to write to the StringBuilder.</param>
        /// <returns>The initialized string builder.</returns>
        private StringBuilder InitializeStringBuilder(System.DateTime timeStamp)
        {
            StringBuilder returnStringBuilder = new StringBuilder();
            returnStringBuilder.Append(timeStamp.ToString(dateTimeFormat));
            AppendSeparatorCharacter(returnStringBuilder);
            return returnStringBuilder;
        }

        /// <summary>
        /// Appends the separator character to a StringBuilder object.
        /// </summary>
        /// <param name="stringBuilder">The StringBuilder to append the separator character to.</param>
        private void AppendSeparatorCharacter(StringBuilder stringBuilder)
        {
            stringBuilder.Append(" ");
            stringBuilder.Append(separatorCharacter);
            stringBuilder.Append(" ");
        }

        #endregion

        #region Finalize / Dispose Methods

        #pragma warning disable 1591
        ~FileMetricLogger()
        {
            Dispose(false);
        }
        #pragma warning restore 1591

        /// <summary>
        /// Provides a method to free unmanaged resources used by this class.
        /// </summary>
        /// <param name="disposing">Whether the method is being called as part of an explicit Dispose routine, and hence whether managed resources should also be freed.</param>
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                try
                {
                    if (disposing)
                    {
                        // Free other state (managed objects).
                        if (streamWriter != null)
                        {
                            streamWriter.Dispose();
                            streamWriter = null;
                        }
                    }
                    // Free your own state (unmanaged objects).

                    // Set large fields to null.
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }
        }

        #endregion
    }
}
