/*
 * Reactive Extensions (Rx) sample of building IQbservable<T> providers.
 * For more information about Rx, see http://msdn.microsoft.com/en-us/devlabs/ee794896.aspx
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Management;
using System.Reflection;

namespace Microsoft.DevLabs.Reactive.Bridge.Instrumentation.Linq
{
    /// <summary>
    /// Representation of a WQL query with a projection.
    /// </summary>
    /// <typeparam name="T">Entity type for queried events.</typeparam>
    /// <typeparam name="R">Type of resulting objects after projection.</typeparam>
    public sealed class WqlProjectedQuery<T, R>
    {
        /// <summary>
        /// WQL query source.
        /// </summary>
        private readonly ISource _source;

        /// <summary>
        /// WITHIN clause value.
        /// </summary>
        private readonly TimeSpan _within;

        /// <summary>
        /// Filter predicate.
        /// </summary>
        private readonly Expression<Func<T, bool>> _filter;

        /// <summary>
        /// Selector function.
        /// </summary>
        private readonly Expression<Func<T, R>> _selector;

        /// <summary>
        /// Creates a new WQL query with a projection.
        /// </summary>
        /// <param name="source">WQL query source.</param>
        /// <param name="within">WITHIN clause value.</param>
        /// <param name="filter">Filter predicate.</param>
        /// <param name="selector">Selector function.</param>
        internal WqlProjectedQuery(ISource source, TimeSpan within, Expression<Func<T, bool>> filter, Expression<Func<T, R>> selector)
        {
            _source = source;
            _within = within;
            _filter = filter;
            _selector = selector;
        }

        /// <summary>
        /// Obtains an observable sequence object to receive the WQL query results.
        /// </summary>
        /// <returns>Observable sequence for query results.</returns>
        public IObservable<R> AsObservable()
        {
            var source = " FROM " + _source.ClassName;

            var filter = "";
            if (_filter != null)
            {
                filter = " WHERE " + VisitFilter(_filter.Body, _filter.Parameters[0]);
            }

            var ctor = default(Func<ManagementBaseObject, R>);
            var project = "SELECT *";
            if (_selector != null)
            {
                project = "SELECT " + VisitSelector(_selector.Body, _selector.Parameters[0], out ctor);
            }

            var within = _within.Seconds > 0 ? " WITHIN " + _within.Seconds : "";

            var query = project + source + within + filter;

            if (_source.Logger != null)
                _source.Logger.WriteLine(query);

            return EventProvider.Receive<R>(query, @"root\cimv2", ctor);
        }

        /// <summary>
        /// Recursive WQL filter translation function.
        /// </summary>
        /// <param name="expression">Expression to be translated.</param>
        /// <param name="parameter">Parameter expression referring to the event class entity being filtered.</param>
        /// <returns>WQL filter for the given expression.</returns>
        private static string VisitFilter(Expression expression, ParameterExpression parameter)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    {
                        var be = (BinaryExpression)expression;
                        return string.Format(CultureInfo.InvariantCulture, "({0}) AND ({1})", VisitFilter(be.Left, parameter), VisitFilter(be.Right, parameter));
                    }
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    {
                        var be = (BinaryExpression)expression;
                        return string.Format(CultureInfo.InvariantCulture, "({0}) OR ({1})", VisitFilter(be.Left, parameter), VisitFilter(be.Right, parameter));
                    }
                case ExpressionType.Not:
                    {
                        var ue = (UnaryExpression)expression;
                        return string.Format(CultureInfo.InvariantCulture, "NOT ({0})", VisitFilter(ue.Operand, parameter));
                    }
                case ExpressionType.LessThan:
                    return VisitFilter("<", expression, parameter);
                case ExpressionType.LessThanOrEqual:
                    return VisitFilter("<=", expression, parameter);
                case ExpressionType.GreaterThan:
                    return VisitFilter(">", expression, parameter);
                case ExpressionType.GreaterThanOrEqual:
                    return VisitFilter(">=", expression, parameter);
                case ExpressionType.Equal:
                    return VisitFilter("=", expression, parameter);
                case ExpressionType.NotEqual:
                    return VisitFilter("!=", expression, parameter);
                default:
                    throw new InvalidOperationException("Unsupported query expression encountered.");
            }
        }

        /// <summary>
        /// Helper function to visit a binary operator used in a filter and return its WQL representation.
        /// </summary>
        /// <param name="op">Symbolic infix operator representation.</param>
        /// <param name="expression">Expression to be translated.</param>
        /// <param name="parameter">Parameter expression referring to the event class entity being filtered.</param>
        /// <returns>WQL filter for the given expression.</returns>
        private static string VisitFilter(string op, Expression expression, ParameterExpression parameter)
        {
            var be = (BinaryExpression)expression;

            var left = VisitFilterOperand(be.Left, parameter);
            var right = VisitFilterOperand(be.Right, parameter);
            if (!(left.HasValue ^ right.HasValue))
                throw new InvalidOperationException("Filter conditions should have at least one reference to a property.");

            return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", left.ToString(), op, right.ToString());
        }

        /// <summary>
        /// Helper function to visit a filter operator's operands and return the containing member name or value.
        /// </summary>
        /// <param name="operand">Expression of the operand to be analyzed.</param>
        /// <param name="parameter">Parameter expression referring to the event class entity being filtered.</param>
        /// <returns>Member of value union type with information extracted from the operand expression.</returns>
        private static MemberOrValue VisitFilterOperand(Expression operand, ParameterExpression parameter)
        {
            var ce = operand as ConstantExpression;
            if (ce != null)
                return new MemberOrValue { Value = ce.Value, HasValue = true };

            var me = operand as MemberExpression;
            if (me != null && me.Expression == parameter)
                return new MemberOrValue { Member = me.Member, HasValue = false };

            // TODO: check no bound parameters; support dot operator
            var val = Expression.Lambda(operand).Compile().DynamicInvoke();
            return new MemberOrValue { Value = val, HasValue = true };
        }

        /// <summary>
        /// Translates the given selector expression to its WQL representation.
        /// </summary>
        /// <param name="expression">Expression of the selector function being translated.</param>
        /// <param name="parameter">Parameter expression referring to the event class entity being projected.</param>
        /// <param name="ctor">Output parameter returning a constructor function to map a ManagementBaseObject on the projected object type. Will be null for an identity function projection.</param>
        /// <returns>WQL representation of the selector function used in a projection.</returns>
        private static string VisitSelector(Expression expression, ParameterExpression parameter, out Func<ManagementBaseObject, R> ctor)
        {
            ctor = null;

            //
            // A null-valued ctor is fine and will be replaced by a function supplying all of the entity's fields.
            //
            if (expression == parameter)
                return "*";

            var ne = expression as NewExpression;
            if (ne != null)
            {
                var ctorArg = Expression.Parameter(typeof(ManagementBaseObject), "o");

                var members = new List<string>();
                var args = new List<Expression>();
                for (int i = 0; i < ne.Members.Count; i++)
                {
                    var arg = ne.Arguments[i];
                    var member = VisitSelectorMember(arg, parameter);
                    members.Add(member);

                    args.Add(
                        Expression.Convert(
                            Expression.MakeIndex(ctorArg, typeof(ManagementBaseObject).GetProperty("Item"), new[] { Expression.Constant(member) }),
                            arg.Type
                        )
                    );
                }

                ctor = Expression.Lambda<Func<ManagementBaseObject, R>>(Expression.New(ne.Constructor, args), ctorArg).Compile();
                return string.Join(", ", members);
            }

            var me = expression as MemberExpression;
            if (me != null)
            {
                var member = VisitSelectorMember(me, parameter);

                var ctorArg = Expression.Parameter(typeof(ManagementBaseObject), "o");
                ctor = Expression.Lambda<Func<ManagementBaseObject, R>>(
                    Expression.Convert(
                        Expression.MakeIndex(ctorArg, typeof(ManagementBaseObject).GetProperty("Item"), new[] { Expression.Constant(member) }),
                        me.Type
                    ), ctorArg).Compile();

                return member;
            }

            throw new InvalidOperationException("Unsupported projection expression encountered.");
        }

        /// <summary>
        /// Visits an expected member expression used in a projection clause to extract an WMI event object's column.
        /// </summary>
        /// <param name="expression">Expression expected to be a member lookup used in a projection clause.</param>
        /// <param name="parameter">Parameter expression referring to the event class entity being projected.</param>
        /// <returns>Name of the member being retrieved.</returns>
        private static string VisitSelectorMember(Expression expression, ParameterExpression parameter)
        {
            //
            // NOTE: No column-level mapping mechanism using a custom attribute exists currently for properties. This is simple to add if desired.
            //
            var me = expression as MemberExpression;
            if (me == null || me.Expression != parameter)
                throw new InvalidOperationException("Unsupported projection expression encountered: expected property reference.");
            else
                return me.Member.Name;
        }

        /// <summary>
        /// Union type representing a member reference or a value.
        /// </summary>
        class MemberOrValue
        {
            /// <summary>
            /// Gets or sets a member info.
            /// </summary>
            public MemberInfo Member { get; set; }

            /// <summary>
            /// Gets or sets whether the object contains a value.
            /// If set to false, Member should be supplied. If set to true, Value should be supplied.
            /// </summary>
            public bool HasValue { get; set; }

            /// <summary>
            /// Gets or sets a value.
            /// </summary>
            public object Value { get; set; }

            /// <summary>
            /// Returns a WQL string representation of the member (column) reference or the value.
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                if (HasValue)
                {
                    if (Value == null)
                        return "NULL";
                    else if (Value is bool)
                        return (bool)Value ? "TRUE" : "FALSE";
                    else if (Value is string)
                        return "\"" + Value.ToString() + "\"";
                    else if (Value is int || Value is uint || Value is short || Value is ushort)
                        return Value.ToString();
                    else
                        throw new InvalidOperationException("Unsupported data type detected."); // TODO: support more types
                }
                else
                    //
                    // NOTE: No column-level mapping mechanism using a custom attribute exists currently for properties. This is simple to add if desired.
                    //
                    return Member.Name;
            }
        }
    }
}
