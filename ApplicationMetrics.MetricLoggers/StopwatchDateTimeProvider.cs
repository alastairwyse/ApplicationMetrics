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
using System.Diagnostics;

namespace ApplicationMetrics.MetricLoggers
{
    /// <summary>
    /// Returns the current date and time using an underlying Stopwatch class for increased accuracy over the DefaultDateTimeProvider class.
    /// </summary>
    public class StopwatchDateTimeProvider : IDateTimeProvider
    {
        /// <summary>Stopwatch to use for calculating the current date and time.</summary>
        protected Stopwatch stopwatch;
        /// <summary>The time at which the stopwatch was started.</summary>
        protected DateTime stopwatchStartTime;

        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.StopwatchDateTimeProvider class.
        /// </summary>
        public StopwatchDateTimeProvider()
        {
            stopwatch = new Stopwatch();
            // There is potential for a gap here between starting the stopwatch and populating 'stopwatchStartTime'
            //   However I think this is acceptable... the goal here is to provide high resolution and accurate timestamps (with respect to the difference between successive gets of Now/UtcNow) moreso than time time being 100% accurate to real-world GMT/UTC...
            //   Hence a small 'gap' is acceptable.
            stopwatch.Start();
            stopwatchStartTime = DateTime.UtcNow;
        }

        /// <inheritdoc/>
        public DateTime UtcNow()
        {
            return stopwatchStartTime.AddTicks(stopwatch.ElapsedTicks);
        }
    }
}
