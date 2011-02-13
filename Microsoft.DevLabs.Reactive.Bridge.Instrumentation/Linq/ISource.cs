/*
 * Reactive Extensions (Rx) sample of building IQbservable<T> providers.
 * For more information about Rx, see http://msdn.microsoft.com/en-us/devlabs/ee794896.aspx
 */

using System;
using System.IO;

namespace Microsoft.DevLabs.Reactive.Bridge.Instrumentation.Linq
{
    /// <summary>
    /// Queryable WMI source.
    /// </summary>
    interface ISource
    {
        /// <summary>
        /// Gets the WMI event class name.
        /// </summary>
        string ClassName { get; }

        /// <summary>
        /// Gets the logger used to trace generated WQL queries.
        /// </summary>
        TextWriter Logger { get; }
    }
}
