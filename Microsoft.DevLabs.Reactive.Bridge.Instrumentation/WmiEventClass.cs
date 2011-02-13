/*
 * Reactive Extensions (Rx) sample of building IQbservable<T> providers.
 * For more information about Rx, see http://msdn.microsoft.com/en-us/devlabs/ee794896.aspx
 */

using System;

namespace Microsoft.DevLabs.Reactive.Bridge.Instrumentation
{
    /// <summary>
    /// Custom attribute used to tag a class with the WMI event it represents.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class WmiEventClassAttribute : Attribute
    {
        /// <summary>
        /// Creates a new WMI event class mapping.
        /// </summary>
        /// <param name="className">Name of the WMI event represented by the class the attribute is applied to.</param>
        public WmiEventClassAttribute(string className)
        {
            ClassName = className;
        }

        /// <summary>
        /// Gets the WMI event class name.
        /// </summary>
        public string ClassName { get; private set; }
    }
}
