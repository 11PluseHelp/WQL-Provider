/*
 * Reactive Extensions (Rx) sample of building IQbservable<T> providers.
 * For more information about Rx, see http://msdn.microsoft.com/en-us/devlabs/ee794896.aspx
 */

using System;
using System.ComponentModel;
using System.Management.Instrumentation;

[assembly: Instrumented("Root/RxBridge/Demo")]

namespace DemoEventProvider
{
    /// <summary>
    /// Installer for the WMI provider.
    /// </summary>
    [RunInstaller(true)]
    public class Installer : DefaultManagementProjectInstaller { }

    /// <summary>
    /// Sample event, simply encapsulating an int32 value.
    /// </summary>
    public class TickEvent : BaseEvent
    {
        /// <summary>
        /// Gets or sets the ticks signaled by the event.
        /// </summary>
        public long Ticks { get; set; }

        /// <summary>
        /// Gets a string representation of the event.
        /// </summary>
        /// <returns>Tick count.</returns>
        public override string ToString()
        {
            return Ticks.ToString();
        }
    }

    /// <summary>
    /// Sample event, signals completion of the provider.
    /// </summary>
    public class StopEvent : BaseEvent
    {
    }
}
