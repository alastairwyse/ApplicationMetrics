/*
 * Copyright 2014 Alastair Wyse (https://github.com/alastairwyse/ApplicationMetrics/)
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
    /// Contains common utility methods used in ApplicationMetrics.MetricLoggers.
    /// </summary>
    public class Utilities
    {
        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.MetricLoggers.Utilities class.
        /// </summary>
        public Utilities()
        {
        }

        /// <summary>
        /// Converts the specified time unit into the equivalent number of milliseconds
        /// </summary>
        /// <param name="timeUnit">The time unit to convert.</param>
        /// <returns>The number of milliseconds.</returns>
        public Int32 ConvertTimeUnitToMilliSeconds(TimeUnit timeUnit)
        {
            switch (timeUnit)
            {
                case TimeUnit.Second:
                    return 1000;

                case TimeUnit.Minute:
                    return 60000;

                case TimeUnit.Hour:
                    return 3600000;

                case TimeUnit.Day:
                    return 86400000;

                default:
                    throw new ArgumentException("Received unhandled timeUnit '" + timeUnit.ToString() + "'.", "timeUnit");
            }
        }
    }
}
