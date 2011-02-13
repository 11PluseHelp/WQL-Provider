/*
 * Reactive Extensions (Rx) sample of building IQbservable<T> providers.
 * For more information about Rx, see http://msdn.microsoft.com/en-us/devlabs/ee794896.aspx
 */

using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Management.Instrumentation;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.DevLabs.Reactive.Bridge.Instrumentation.Linq;

namespace Microsoft.DevLabs.Reactive.Bridge.Instrumentation
{
    /// <summary>
    /// Provides facilities to expose an observable data source through WMI and to consume WMI events using Rx.
    /// </summary>
    public static class EventProvider
    {
        /// <summary>
        /// Submits an observable source of WMI events to WMI.
        /// </summary>
        /// <typeparam name="T">Type of the event; derives from BaseEvent.</typeparam>
        /// <param name="source">Source of WMI events.</param>
        /// <returns>Handle to stop the publication of the specified source.</returns>
        public static IDisposable Submit<T>(this IObservable<T> source) where  T : BaseEvent
        {
            return source.Subscribe(item =>
            {
                item.Fire();
            });
        }

        /// <summary>
        /// Receives WMI events of the given type.
        /// </summary>
        /// <typeparam name="T">Type of the event; derives from BaseEvent.</typeparam>
        /// <returns>Observable data source with the WMI events.</returns>
        public static IObservable<T> Receive<T>() where T : BaseEvent
        {
            var type = typeof(T);

            var ns = @"root\cimv2";
            var instr = type.Assembly.GetCustomAttributes(typeof(InstrumentedAttribute), false).OfType<InstrumentedAttribute>().SingleOrDefault();
            if (instr != null)
                ns = instr.NamespaceName;

            return Receive<T>("SELECT * FROM " + type.Name, ns);
        }

        /// <summary>
        /// Runs a WQL query and exposes the results as an observable sequence.
        /// </summary>
        /// <typeparam name="T">Type of the event data.</typeparam>
        /// <param name="wqlQuery">WQL query.</param>
        /// <param name="scope">WMI namespace.</param>
        /// <returns>Observable sequence with the query results.</returns>
        public static IObservable<T> Receive<T>(string wqlQuery, string scope)
        {
            return Receive<T>(wqlQuery, scope, null);
        }

        /// <summary>
        /// Entry-point for a LINQ query that executes WMI queries.
        /// This method exposes a source implementing the query pattern.
        /// </summary>
        /// <typeparam name="T">Type of the event data to query over.</typeparam>
        /// <param name="className">WMI event class.</param>
        /// <param name="prototype">Prototype of a query object to infer <typeparamref name="T">T</typeparamref> from.</param>
        /// <param name="logger">Optional logger for generated WQL queries.</param>
        /// <returns>Queryable object to build a LINQ query on. Turn the query into an observable sequence by using AsObservable.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "prototype")]
        public static WqlQuerySource<T> Query<T>(string className = null, T prototype = default(T), TextWriter logger = null)
        {
            var @class = className ?? GetClassName<T>();
            return new WqlQuerySource<T>(@class, logger);
        }

        /// <summary>
        /// Entry-point for a LINQ query that executes WMI queries.
        /// This method exposes a source implementing the IQbservable interface.
        /// </summary>
        /// <typeparam name="T">Type of the event data to query over.</typeparam>
        /// <param name="className">WMI event class.</param>
        /// <param name="prototype">Prototype of a query object to infer <typeparamref name="T">T</typeparamref> from.</param>
        /// <param name="logger">Optional logger for generated WQL queries.</param>
        /// <returns>Queryable object to build a LINQ query on. Turn the query into an observable sequence by using AsObservable.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "prototype"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Qbserve")]
        public static WqlQbservableSource<T> Qbserve<T>(string className = null, T prototype = default(T), TextWriter logger = null)
        {
            var @class = className == null ? GetClassName<T>() : className;
            return new WqlQbservableSource<T>(@class, logger);
        }

        /// <summary>
        /// Gets the WMI event class name from the given type based on an optional mapping.
        /// </summary>
        /// <typeparam name="T">Entity type to get the corresponding WMI event class name for.</typeparam>
        /// <returns>WMI event class name for the <typeparamref name="T">entity type</typeparamref>.</returns>
        private static string GetClassName<T>()
        {
            var t = typeof(T);

            var a = t.GetCustomAttributes(typeof(WmiEventClassAttribute), false).Cast<WmiEventClassAttribute>().SingleOrDefault();
            if (a == null)
                return t.Name;
            else
                return a.ClassName;
        }

        /// <summary>
        /// Creates an observable sequence for the given <paramref name="wqlQuery">WQL query</paramref> and an <paramref name="ctor">entity constructor function</paramref>.
        /// </summary>
        /// <typeparam name="T">Entity type used for the results.</typeparam>
        /// <param name="wqlQuery">WQL event query to watch.</param>
        /// <param name="scope">Scope used for WMI event watcher.</param>
        /// <param name="ctor">Entity constructor function.</param>
        /// <returns>Observable sequence with event query results.</returns>
        internal static IObservable<T> Receive<T>(string wqlQuery, string scope, Func<ManagementBaseObject, T> ctor)
        {
            if (ctor == null)
            {
                var type = typeof(T);

                ctor = o =>
                {
                    var res = (T)FormatterServices.GetUninitializedObject(type);

                    var fields = default(FieldInfo[]);

                    foreach (var prop in o.Properties)
                    {
                        var pi = type.GetProperty(prop.Name);
                        if (pi != null)
                        {
                            var set = pi.GetSetMethod();
                            if (set != null)
                            {
                                pi.SetValue(res, prop.Value, null);
                            }
                            else
                            {
                                if (fields == null)
                                {
                                    fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
                                }

                                var field = fields.SingleOrDefault(fld => fld.Name.StartsWith("<" + prop.Name + ">"));
                                if (field != null)
                                {
                                    field.SetValue(res, prop.Value);
                                }
                                else
                                    throw new InvalidOperationException("Failed to instantiate result object. No suitable property or field found.");
                            }
                        }
                    }

                    return res;
                };
            }

            return Observable.Create<T>(obs =>
            {
                var watcher = new ManagementEventWatcher(scope, wqlQuery);
                watcher.Start();
                watcher.EventArrived += (o, e) => obs.OnNext(ctor(e.NewEvent));
                return () => { watcher.Stop(); watcher.Dispose(); };
            });
        }
    }
}
