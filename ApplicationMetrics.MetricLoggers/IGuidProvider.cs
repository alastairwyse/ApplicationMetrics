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

namespace ApplicationMetrics.MetricLoggers
{
    /// <summary>
    /// Defines a method which returns random <see cref="Guid">Guids</see>.
    /// </summary>
    public interface IGuidProvider
    {
        /// <summary>
        /// Creates a new random <see cref="Guid"/>.
        /// </summary>
        /// <returns>A new <see cref="Guid"/>.</returns>
        Guid NewGuid();
    }
}
