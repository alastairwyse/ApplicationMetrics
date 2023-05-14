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
    /// Defines methods which interact with a strategy to process buffered metric events in a MetricLoggerBuffer class.  MetricLoggerBuffer classes utilizing the strategy should call the releavant 'Notify' methods as necessary, and subscribe to event <see cref="IBufferProcessingStrategy.BufferProcessed"/> to be notified of when to process the contents of their buffers.
    /// </summary>
    public interface IBufferProcessingStrategy
    {
        /// <summary>
        /// Occurs when the metric events stored in the buffer are removed from the buffer and processed.
        /// </summary>
        event EventHandler BufferProcessed;

        /// <summary>
        /// Starts the buffer processing (e.g. if the implementation of the strategy uses a worker thread, this method starts the worker thread).
        /// </summary>
        void Start();

        /// <summary>
        /// Processes any metric events remaining in the buffers, and stops the buffer processing (e.g. if the implementation of the strategy uses a worker thread, this method stops the worker thread).
        /// </summary>
        void Stop();

        /// <summary>
        /// Stops the buffer processing (e.g. if the implementation of the strategy uses a worker thread, this method stops the worker thread).
        /// </summary>
        /// <remarks>There may be cases where the client code does not want to process any remaining metric events, e.g. in the case that the method was called as part of an exception handling routine.  This overload of the Stop() method allows the client code to specify this behaviour.</remarks>
        /// <param name="processRemainingBufferedMetricEvents">Whether any metric events remaining in the buffers should be processed.</param>
        void Stop(bool processRemainingBufferedMetricEvents);

        /// <summary>
        /// Notifies the buffer processing strategy that a count metric event was added to the buffer.
        /// </summary>
        void NotifyCountMetricEventBuffered();

        /// <summary>
        /// Notifies the buffer processing strategy that an amount metric event was added to the buffer.
        /// </summary>
        void NotifyAmountMetricEventBuffered();

        /// <summary>
        /// Notifies the buffer processing strategy that a status metric event was added to the buffer.
        /// </summary>
        void NotifyStatusMetricEventBuffered();

        /// <summary>
        /// Notifies the buffer processing strategy that an interval metric event was added to the buffer.
        /// </summary>
        void NotifyIntervalMetricEventBuffered();

        /// <summary>
        /// Notifies the buffer processing strategy that the buffer holding count metric events was cleared.
        /// </summary>
        void NotifyCountMetricEventBufferCleared();

        /// <summary>
        /// Notifies the buffer processing strategy that the buffer holding amount metric events was cleared.
        /// </summary>
        void NotifyAmountMetricEventBufferCleared();

        /// <summary>
        /// Notifies the buffer processing strategy that the buffer holding status metric events was cleared.
        /// </summary>
        void NotifyStatusMetricEventBufferCleared();

        /// <summary>
        /// Notifies the buffer processing strategy that the buffer holding interval metric events was cleared.
        /// </summary>
        void NotifyIntervalMetricEventBufferCleared();
    }
}
