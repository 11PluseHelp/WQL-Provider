/*
 * Reactive Extensions (Rx) sample of building IQbservable<T> providers.
 * For more information about Rx, see http://msdn.microsoft.com/en-us/devlabs/ee794896.aspx
 */

using System;
using System.IO;
using System.Linq.Expressions;

namespace Microsoft.DevLabs.Reactive.Bridge.Instrumentation.Linq
{
    /// <summary>
    /// Represents a WQL event query source.
    /// </summary>
    /// <typeparam name="T">Entity type representing the WMI event class being queried.</typeparam>
    public sealed class WqlQuerySource<T> : ISource
    {
        /// <summary>
        /// Creates a new WQL event query source.
        /// </summary>
        /// <param name="className">WMI event class name.</param>
        /// <param name="logger">Logger used to trace generated WQL queries.</param>
        internal WqlQuerySource(string className, TextWriter logger)
        {
            ClassName = className;
            Logger = logger;
        }

        /// <summary>
        /// Gets the WMI event class name.
        /// </summary>
        public string ClassName { get; private set; }

        /// <summary>
        /// Gets the logger used to trace generated WQL queries.
        /// </summary>
        public TextWriter Logger { get; private set; }

        /// <summary>
        /// Applies a WITHIN clause to the query.
        /// </summary>
        /// <param name="timeSpan">WITHIN clause value.</param>
        /// <returns>Representation of the query with a WITHIN clause applied.</returns>
        public WqlWithinQuery<T> Within(TimeSpan timeSpan)
        {
            return new WqlWithinQuery<T>(this, timeSpan);
        }

        /// <summary>
        /// Applies a filter to the query.
        /// </summary>
        /// <param name="filter">Filter predicate.</param>
        /// <returns>Representation of the query with a filter applied.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        public WqlFilteredQuery<T> Where(Expression<Func<T, bool>> filter)
        {
            return new WqlFilteredQuery<T>(this, TimeSpan.Zero, filter);
        }

        /// <summary>
        /// Applies a projection clause to the query.
        /// </summary>
        /// <typeparam name="R">Projection result type.</typeparam>
        /// <param name="selector">Selector function.</param>
        /// <returns>Representation of the query with a projection clause applied.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
        public WqlProjectedQuery<T, R> Select<R>(Expression<Func<T, R>> selector)
        {
            return new WqlProjectedQuery<T, R>(this, TimeSpan.Zero, null, selector);
        }

        /// <summary>
        /// Obtains an observable sequence object to receive the WQL query results.
        /// </summary>
        /// <returns>Observable sequence for query results.</returns>
        public IObservable<T> AsObservable()
        {
            return new WqlProjectedQuery<T, T>(this, TimeSpan.Zero, null, null).AsObservable();
        }
    }
}
