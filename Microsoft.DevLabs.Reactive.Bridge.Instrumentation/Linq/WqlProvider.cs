/*
 * Reactive Extensions (Rx) sample of building IQbservable<T> providers.
 * For more information about Rx, see http://msdn.microsoft.com/en-us/devlabs/ee794896.aspx
 */

using System;
using System.Linq.Expressions;

namespace Microsoft.DevLabs.Reactive.Bridge.Instrumentation.Linq
{
    /// <summary>
    /// Qbservable query provider for WQL.
    /// </summary>
    public sealed class WqlProvider : IQbservableProvider
    {
        /// <summary>
        /// Singleton instance of the provider.
        /// </summary>
        private static Lazy<WqlProvider> s_instance = new Lazy<WqlProvider>(() => new WqlProvider());

        /// <summary>
        /// Private constructor to enable factory pattern.
        /// </summary>
        private WqlProvider()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the provider.
        /// </summary>
        /// <remarks>
        /// This object is publicly exposed and can be used to access certain operators (or specific overloads thereof) that don't have a single source sequence (e.g. Amb).
        /// For WQL, no such operators are currently supported, though we show the exposure of the provider instance nonetheless.
        /// </remarks>
        public static WqlProvider Instance
        {
            get { return s_instance.Value; }
        }

        /// <summary>
        /// Creates a new query targeting the WQL qbservable query provider usign the given expression.
        /// </summary>
        /// <typeparam name="TResult">Entity result type of the query represented by the expression.</typeparam>
        /// <param name="expression">Expression representing the query to be created as a qbservable object.</param>
        /// <returns>Qbservable representation of the given query expression.</returns>
        public IQbservable<TResult> CreateQuery<TResult>(Expression expression)
        {
            return new WqlQbservableSource<TResult>(expression);
        }
    }
}
