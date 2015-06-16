/*
 * Copyright 2014 Alastair Wyse (http://www.oraclepermissiongenerator.net/methodinvocationremoting/)
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
using System.Collections.Generic;
using System.Text;

namespace ApplicationMetrics
{
    //******************************************************************************
    //
    // Class: ExceptionThrower
    //
    //******************************************************************************
    /// <summary>
    /// Throws any exceptions passed to the Handle() method.
    /// </summary>
    /// <remarks>For classes in this project which have code running in worker threads, an instance of this class is used to throw any exceptions that occur.  For unit tests, a mock of IExceptionHandler is injected into the class so that exceptions occurring on the worker thread can be intercepted and verified.</remarks>
    class ExceptionThrower : IExceptionHandler
    {
        //------------------------------------------------------------------------------
        //
        // Method: ExceptionThrower (constructor)
        //
        //------------------------------------------------------------------------------
        /// <summary>
        /// Initialises a new instance of the ApplicationMetrics.ExceptionThrower class.
        /// </summary>
        public ExceptionThrower()
        {
        }

        /// <include file='InterfaceDocumentationComments.xml' path='doc/members/member[@name="M:ApplicationMetrics.IExceptionHandler.Handle(System.Exception)"]/*'/>
        public void Handle(Exception e)
        {
            throw (e);
        }
    }
}
