/*
 * Reactive Extensions (Rx) sample of building IQbservable<T> providers.
 * For more information about Rx, see http://msdn.microsoft.com/en-us/devlabs/ee794896.aspx
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.DevLabs.Reactive.Bridge.Instrumentation.Linq
{
    /// <summary>
    /// Represents a qbservable WQL event query source.
    /// </summary>
    /// <typeparam name="T">Entity type representing the WMI event class being queried.</typeparam>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Qbservable")]
    public sealed class WqlQbservableSource<T> : ISource, IQbservable<T>
    {
        /// <summary>
        /// Query expression.
        /// </summary>
        private readonly Expression _expression;

        /// <summary>
        /// Creates a new qbservable WQL event query source for the given WQL event class and an optional logger.
        /// </summary>
        /// <param name="className">WMI event class name.</param>
        /// <param name="logger">Logger used to trace generated WQL queries.</param>
        internal WqlQbservableSource(string className, TextWriter logger)
        {
            ClassName = className;
            Logger = logger;

            _expression = Expression.Constant(this);
        }

        /// <summary>
        /// Creates a new qbservable WQL event query represented by the given expression.
        /// </summary>
        /// <param name="expression">Query expression.</param>
        internal WqlQbservableSource(Expression expression)
        {
            _expression = expression;
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
        /// Gets the entity type representing the WMI event class being queried.
        /// </summary>
        public Type ElementType
        {
            get { return typeof(T); }
        }

        /// <summary>
        /// Gets the query expression.
        /// </summary>
        public Expression Expression
        {
            get { return _expression; }
        }

        /// <summary>
        /// Gets the WQL query provider.
        /// </summary>
        public IQbservableProvider Provider
        {
            get { return WqlProvider.Instance; }
        }

        /// <summary>
        /// Subscribe method triggering translation of the query expression into WQL, receiving events on the given observer.
        /// </summary>
        /// <param name="observer">Observer to receive WMI events on.</param>
        /// <returns>IDisposable object to unsubscribe from the event source.</returns>
        public IDisposable Subscribe(IObserver<T> observer)
        {
            //
            // To keep things simple, we'll expand the expression tree into a WqlQuerySource<T>-based pattern,
            // where we've implemented the query translation already. We assume the expression tree to be
            // constructed correctly over the WqlQbservableSource<T> source object.
            //

            var ops = FindOperators.Find(_expression);
            var src = (ISource)((Source)ops[0]).Value;
            var elt = src.GetType().GetGenericArguments()[0];
            var res = Activator.CreateInstance(typeof(WqlQuerySource<>).MakeGenericType(elt), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { src.ClassName, src.Logger }, null);

            var exp = (Expression)Expression.Constant(res);
            for (int i = 1; i < ops.Count; i++)
            {
                if (ops[i] is Where)
                {
                    var flt = ((Where)ops[i]).Filter;
                    var whr = exp.Type.GetMethod("Where");
                    exp = Expression.Call(exp, whr, flt);
                }
                else if (ops[i] is Select)
                {
                    var str = ((Select)ops[i]).Selector;
                    var sel = exp.Type.GetMethod("Select").MakeGenericMethod(str.ReturnType);
                    exp = Expression.Call(exp, sel, str);
                }
                else
                    throw new NotSupportedException("Unsupported operator encountered.");
            }

            exp = Expression.Call(exp, exp.Type.GetMethod("AsObservable"));

            var obs = Expression.Lambda<Func<IObservable<T>>>(Expression.Convert(exp, typeof(IObservable<T>))).Compile()();
            return obs.Subscribe(observer);
        }

        /// <summary>
        /// Base type for data representation of operator uses detected in a query expression.
        /// </summary>
        class Operator
        {
        }

        /// <summary>
        /// Where operator data representation.
        /// </summary>
        class Where : Operator
        {
            /// <summary>
            /// Creates a new Where operator data representation.
            /// </summary>
            /// <param name="filter">Filter expression.</param>
            public Where(LambdaExpression filter)
            {
                Filter = filter;
            }

            /// <summary>
            /// Gets the filter expression.
            /// </summary>
            public LambdaExpression Filter { get; private set; }
        }

        /// <summary>
        /// Select operator data representation.
        /// </summary>
        class Select : Operator
        {
            /// <summary>
            /// Creates a new Select operator data representation.
            /// </summary>
            /// <param name="selector">Selector expression.</param>
            public Select(LambdaExpression selector)
            {
                Selector = selector;
            }

            /// <summary>
            /// Gets the selector expression.
            /// </summary>
            public LambdaExpression Selector { get; private set; }
        }

        /// <summary>
        /// Source data representation (as a pseudo-operator).
        /// </summary>
        class Source : Operator
        {
            public Source(object source)
            {
                Value = source;
            }

            public object Value { get; private set; }
        }

        /// <summary>
        /// Visitor to find supported query operators in a WQL event query.
        /// </summary>
        class FindOperators : ExpressionVisitor
        {
            /// <summary>
            /// Qbservable's generic Where operator method.
            /// </summary>
            private static MethodInfo s_where = Helpers.InfoOf((IQbservable<int> o) => o.Where(i => true)).GetGenericMethodDefinition();

            /// <summary>
            /// Qbservable's generic Select operator method.
            /// </summary>
            private static MethodInfo s_select = Helpers.InfoOf((IQbservable<int> o) => o.Select(i => i)).GetGenericMethodDefinition();

            /// <summary>
            /// Private constructor. Use the Find method to create an instance and use it.
            /// </summary>
            private FindOperators()
            {
                Result = new List<Operator>();
            }

            /// <summary>
            /// Finds the chain of query operators in the given expression.
            /// </summary>
            /// <param name="expression">Expression to search for query operator uses.</param>
            /// <returns>List of operators in the order they occur in the query expression.</returns>
            public static List<Operator> Find(Expression expression)
            {
                var finder = new FindOperators();
                finder.Visit(expression);
                return finder.Result;
            }

            /// <summary>
            /// Gets the list of operators in the order they occur in the query expression.
            /// </summary>
            public List<Operator> Result { get; private set; }

            /// <summary>
            /// Visit method to detect the constant node used for the query source object.
            /// </summary>
            /// <param name="node">Constant expression node.</param>
            /// <returns>The original node.</returns>
            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (node.Value is ISource)
                {
                    Result.Add(new Source(node.Value));
                    return node;
                }

                throw new NotSupportedException("Unknown query source.");
            }

            /// <summary>
            /// Visit method to detect Where and Select query operator nodes.
            /// </summary>
            /// <param name="node">Method call expression node.</param>
            /// <returns>The original node.</returns>
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Method.IsGenericMethod)
                {
                    var method = node.Method.GetGenericMethodDefinition();
                    if (method == s_where)
                    {
                        Visit(node.Arguments[0]);
                        Result.Add(new Where((LambdaExpression)((UnaryExpression)node.Arguments[1]).Operand));
                        return node;
                    }
                    else if (method == s_select)
                    {
                        Visit(node.Arguments[0]);
                        Result.Add(new Select((LambdaExpression)((UnaryExpression)node.Arguments[1]).Operand));
                        return node;
                    }
                }

                throw new NotSupportedException("Unsupported operator call: " + node.Method.Name);
            }
        }
    }
}
