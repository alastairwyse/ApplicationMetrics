/*
 * Copyright 2022 Alastair Wyse (https://github.com/alastairwyse/ApplicationMetrics/)
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

namespace ApplicationMetrics.Filters.UnitTests
{
    /// <summary>
    /// Count metric which represents a single disk read operation.
    /// </summary>
    class DiskReadOperation : CountMetric
    {
        protected static String staticName = "DiskReadOperation";
        protected static String staticDescription = "Represents a single disk read operation.";

        public DiskReadOperation()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Count metric which represents receiving a message from an external source.
    /// </summary>
    class MessageReceived : CountMetric
    {
        protected static String staticName = "MessageReceived";
        protected static String staticDescription = "Represents receiving a message from an external source.";

        public MessageReceived()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Amount metric which represents the number of bytes read during a disk read operation.
    /// </summary>
    class DiskBytesRead : AmountMetric
    {
        protected static String staticName = "DiskBytesRead";
        protected static String staticDescription = "Represents the number of bytes read during a disk read operation.";

        public DiskBytesRead()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }
    /// <summary>
    /// Amount metric which represents the number of bytes received when receiving a message from an external source.
    /// </summary>
    class MessageBytesReceived : AmountMetric
    {
        protected static String staticName = "MessageBytesReceived";
        protected static String staticDescription = "Represents the number of bytes received when receiving a message from an external source.";

        public MessageBytesReceived()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Status metric which represents the amount of free memory in the system at a given time.
    /// </summary>
    class AvailableMemory : StatusMetric
    {
        protected static String staticName = "AvailableMemory";
        protected static String staticDescription = "Represents the amount of free memory in the system at a given time.";

        public AvailableMemory()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Status metric which represents the available free worker threads in the application at a given time.
    /// </summary>
    class FreeWorkerThreads : StatusMetric
    {
        protected static String staticName = "FreeWorkerThreads";
        protected static String staticDescription = "Represents the available free worker threads in the application at a given time.";

        public FreeWorkerThreads()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Interval metric which represents the amount of time taken to perform a read operation from disk.
    /// </summary>
    class DiskReadTime : IntervalMetric
    {
        protected static String staticName = "DiskReadTime";
        protected static String staticDescription = "Represents the amount of time taken to perform a read operation from disk.";

        public DiskReadTime()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Interval metric which represents the amount of time taken to perform a write operation to disk.
    /// </summary>
    class DiskWriteTime : IntervalMetric
    {
        protected static String staticName = "DiskWriteTime";
        protected static String staticDescription = "Represents the amount of time taken to perform a write operation to disk.";

        public DiskWriteTime()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }

    /// <summary>
    /// Interval metric which represents the amount of time taken to process a single message received from a remote source.
    /// </summary>
    class MessageProcessingTime : IntervalMetric
    {
        protected static String staticName = "MessageProcessingTime";
        protected static String staticDescription = "Represents the amount of time taken to process a single message received from a remote source.";

        public MessageProcessingTime()
        {
            base.name = staticName;
            base.description = staticDescription;
        }
    }
}
