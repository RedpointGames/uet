namespace Redpoint.CloudFramework.Repository.Converters.Expression
{
    using Google.Cloud.Datastore.V1;
    using Google.Type;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Converters.Timestamp;
    using Redpoint.StringEnum;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    // @note: This implementation has to be completely reworked, since it doesn't use IValueConverters yet.

    internal class DefaultExpressionConverter : IExpressionConverter
    {
        private readonly IInstantTimestampConverter _instantTimestampConverter;
        private readonly Dictionary<Type, MethodInfo> _valueConverters;

        public DefaultExpressionConverter(
            IInstantTimestampConverter instantTimestampConverter)
        {
            _instantTimestampConverter = instantTimestampConverter;

            _valueConverters = new Dictionary<Type, MethodInfo>();
            foreach (var converter in typeof(Value).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(x => x.Name == "op_Implicit" && x.GetParameters().Length == 1 && x.ReturnType == typeof(Value)))
            {
                _valueConverters.Add(converter.GetParameters()[0].ParameterType, converter);
            }
        }

        private string GetFieldReferencedInExpression<T>(Expression expression, ParameterExpression modelExpression, T referenceModel) where T : IModel
        {
            if (expression.NodeType == ExpressionType.MemberAccess)
            {
                var access = (MemberExpression)expression;
                if (access.Expression != modelExpression)
                {
                    // Support for embedded entity queries.
                    if (access.Expression!.NodeType == ExpressionType.Call)
                    {
                        var callAccess = (MethodCallExpression)access.Expression;
                        if (callAccess.Method == typeof(Entity).GetMethod("get_Item", BindingFlags.Public | BindingFlags.Instance))
                        {
                            // This is sub-access on an entity.
                            var subpropertyName = Expression.Lambda<Func<string>>(callAccess.Arguments[0]).Compile()();
                            return GetFieldReferencedInExpression<T>(
                                callAccess.Object!,
                                modelExpression,
                                referenceModel) + "." + subpropertyName;
                        }
                    }

                    throw new InvalidOperationException($"Expression must be a member access operation, and the expression that the member access is being performed on must be the model parameter expression. It was a '{access.Expression.NodeType}' type expression instead.");
                }

                if (access.Member.Name == nameof(IModel.Key))
                {
                    throw new InvalidOperationException($"The 'Key' property can only have the 'HasAncestor' extension method called on it; it can not be used in a comparison.");
                }
                else
                {
                    if (access.Member.Name == nameof(IModel.dateCreatedUtc) ||
                        access.Member.Name == nameof(IModel.dateModifiedUtc) ||
                        (referenceModel.GetIndexes().Contains(access.Member.Name) &&
                         referenceModel.GetTypes().ContainsKey(access.Member.Name)))
                    {
                        return access.Member.Name;
                    }

                    if (access.Member.Name == nameof(IModel.schemaVersion))
                    {
                        return nameof(IModel.schemaVersion);
                    }
                }

                throw new InvalidOperationException($"Expression must be a member access operation, and the member being access must be an indexed field or the 'Key' property. It was '{access.Member.Name}' instead, which is not an indexed field (as per GetIndexes()).");
            }
            else
            {
                throw new InvalidOperationException($"Expression must be a member access operation (like 'x.field'). It was an '{expression.NodeType}' expression instead.");
            }
        }

        private Value? EvaluateExpressionRHSToValue(Expression toEvaluate)
        {
            var valueRaw = Expression.Lambda<Func<object>>(Expression.Convert(toEvaluate, typeof(object))).Compile()();

            if (valueRaw == null)
            {
                return null;
            }

            var valueType = valueRaw.GetType();

            if (valueType == typeof(NodaTime.Instant))
            {
                return _instantTimestampConverter.FromNodaTimeInstantToDatastoreValue((NodaTime.Instant)valueRaw, false);
            }
            else if (valueType == typeof(ulong))
            {
                return unchecked((long)(ulong)valueRaw);
            }
            else if (valueType == typeof(ulong?))
            {
                return unchecked((long?)(ulong?)valueRaw);
            }
            else if (valueType.IsConstructedGenericType &&
                valueType.GetGenericTypeDefinition() == typeof(StringEnumValue<>))
            {
                return new Value
                {
                    // We know ToString() for StringEnumValue<> gives us the enumeration value
                    // for Datastore.
                    StringValue = valueRaw.ToString(),
                };
            }
            else if (_valueConverters.TryGetValue(valueType, out MethodInfo? converterMethod))
            {
                return (Value)converterMethod.Invoke(null, new object[] { valueRaw })!;
            }
            else
            {
                throw new InvalidOperationException($"The RHS expression '{toEvaluate}' evaluates to a value with a type of '{valueRaw.GetType()}', which can not be converted into a Datastore value.");
            }
        }

        public Filter? ConvertExpressionToFilter<T>(Expression expression, ParameterExpression modelExpression, T referenceModel, ref GeoQueryParameters<T>? geoParameters, ref bool hasAncestorQuery) where T : IModel
        {
            if (expression.NodeType == ExpressionType.Constant && ((ConstantExpression)expression).Type == typeof(bool) && (bool)((ConstantExpression)expression).Value! == true)
            {
                // Match everything.
                return null;
            }
            else if (expression.NodeType == ExpressionType.AndAlso)
            {
                // We check for nulls here, since geopoint filters will return null (there's no direct mapping of a
                // geopoint filter onto the Filter class).
                var binaryExpression = (BinaryExpression)expression;
                var lhs = ConvertExpressionToFilter<T>(binaryExpression.Left, modelExpression, referenceModel, ref geoParameters, ref hasAncestorQuery);
                var rhs = ConvertExpressionToFilter<T>(binaryExpression.Right, modelExpression, referenceModel, ref geoParameters, ref hasAncestorQuery);
                if (lhs == null && rhs == null)
                {
                    throw new ArgumentNullException(nameof(expression), "Expected at least one side of an && expression to have a non-geo filter.");
                }
                if (lhs == null) return rhs;
                if (rhs == null) return lhs;
                return Filter.And(lhs, rhs);
            }
            else if (expression.NodeType == ExpressionType.Call)
            {
                var callExpression = (MethodCallExpression)expression;

                if (callExpression.Object != null)
                {
                    // Not an extension method call.
                    throw new InvalidOperationException($"You can only call supported extension methods on specific field types in queries; invoke unsupported '{callExpression.Method.Name}' method.");
                }

                var targetExpression = callExpression.Arguments[0];
                var parentExpression = callExpression.Arguments[1];

                if (targetExpression.NodeType != ExpressionType.MemberAccess)
                {
                    throw new InvalidOperationException($"The only supported method call expressions must be made directly on a model's property or Key. Attempted to invoke a method call on expression with type '{targetExpression.NodeType}'.");
                }

                var targetMemberAccess = (MemberExpression)targetExpression;
                if (targetMemberAccess.Expression != modelExpression ||
                    !(targetMemberAccess.Member is PropertyInfo))
                {
                    throw new InvalidOperationException($"The only supported method call expressions must be made directly on a model's property or Key. Attempted to use member access on base expression of type '{targetMemberAccess.Expression?.NodeType}'.");
                }

                var propertyInfo = (PropertyInfo)targetMemberAccess.Member;

                if (callExpression.Method == typeof(RepositoryExtensions).GetMethod(nameof(RepositoryExtensions.HasAncestor), BindingFlags.Static | BindingFlags.Public) &&
                    callExpression.Arguments.Count == 2)
                {
                    if (propertyInfo.Name != nameof(IModel.Key))
                    {
                        throw new InvalidOperationException($"You can only use 'HasAncestor' on the primary Key and not key properties. Attempted to use member access on property named '{propertyInfo.Name}'.");
                    }

                    hasAncestorQuery = true;
                    return Filter.HasAncestor(EvaluateExpressionRHSToValue(parentExpression)?.KeyValue);
                }
                else if (
                    callExpression.Method == typeof(GeoExtensions).GetMethod(nameof(GeoExtensions.WithinKilometers), BindingFlags.Static | BindingFlags.Public) &&
                    callExpression.Arguments.Count == 3)
                {
                    if (propertyInfo.PropertyType != typeof(LatLng))
                    {
                        throw new InvalidOperationException($"You can only use 'WithinKilometers' on geopoint properties. Attempted to use member access on property named '{propertyInfo.Name}'.");
                    }

                    if (geoParameters != null)
                    {
                        throw new InvalidOperationException($"You can only use a single geopoint filter in a query (you can only call 'WithinKilometers' once in the expression).");
                    }

                    var centerLatLng = Expression.Lambda<Func<LatLng>>(callExpression.Arguments[1]).Compile()();
                    var distanceKilometers = Expression.Lambda<Func<float>>(callExpression.Arguments[2]).Compile()();
                    var serverSideFilter = Expression.Lambda<Func<T, bool>>(callExpression, modelExpression).Compile();
                    var serverSideAccessor = Expression.Lambda<Func<T, LatLng>>(targetExpression, modelExpression).Compile();

                    geoParameters = new GeoQueryParameters<T>
                    {
                        GeoFieldName = propertyInfo.Name,
                        CenterPoint = centerLatLng,
                        MinPoint = GeoExtensions.GetRectangularMinPoint(centerLatLng, distanceKilometers),
                        MaxPoint = GeoExtensions.GetRectangularMaxPoint(centerLatLng, distanceKilometers),
                        ServerSideFilter = serverSideFilter,
                        ServerSideAccessor = serverSideAccessor,
                        DistanceKm = distanceKilometers,
                    };
                    return null;
                }
                else if (callExpression.Method == typeof(RepositoryExtensions).GetMethod(nameof(RepositoryExtensions.IsAnyString), BindingFlags.Static | BindingFlags.Public) &&
                    callExpression.Arguments.Count == 2)
                {
                    var targetValue = Expression.Lambda<Func<string>>(callExpression.Arguments[1]).Compile()();
                    return Filter.Equal(propertyInfo.Name, targetValue);
                }
                else if (callExpression.Method == typeof(RepositoryExtensions).GetMethod(nameof(RepositoryExtensions.IsOneOfString), BindingFlags.Static | BindingFlags.Public) &&
                    callExpression.Arguments.Count == 2)
                {
                    var targetValue = Expression.Lambda<Func<string[]>>(callExpression.Arguments[1]).Compile()();
                    return Filter.In(propertyInfo.Name, targetValue);
                }
                else if (callExpression.Method == typeof(RepositoryExtensions).GetMethod(nameof(RepositoryExtensions.IsNotOneOfString), BindingFlags.Static | BindingFlags.Public) &&
                    callExpression.Arguments.Count == 2)
                {
                    var targetValue = Expression.Lambda<Func<string[]>>(callExpression.Arguments[1]).Compile()();
                    return Filter.NotIn(propertyInfo.Name, targetValue);
                }
                else
                {
                    throw new InvalidOperationException($"The only supported method call expressions are calling 'HasAncestor' on the primary Key and calling 'WithinKilometers' on geopoint properties. Attempted to invoke unsupported '{callExpression.Method.Name}' method.");
                }
            }
            else if (
                expression.NodeType == ExpressionType.Equal ||
                expression.NodeType == ExpressionType.LessThan ||
                expression.NodeType == ExpressionType.LessThanOrEqual ||
                expression.NodeType == ExpressionType.GreaterThan ||
                expression.NodeType == ExpressionType.GreaterThanOrEqual)
            {
                var binaryExpression = (BinaryExpression)expression;
                var field = GetFieldReferencedInExpression(binaryExpression.Left, modelExpression, referenceModel);
                var value = EvaluateExpressionRHSToValue(binaryExpression.Right);

                switch (expression.NodeType)
                {
                    case ExpressionType.Equal:
                        return Filter.Property(field, value, PropertyFilter.Types.Operator.Equal);
                    case ExpressionType.GreaterThan:
                        return Filter.Property(field, value, PropertyFilter.Types.Operator.GreaterThan);
                    case ExpressionType.GreaterThanOrEqual:
                        return Filter.Property(field, value, PropertyFilter.Types.Operator.GreaterThanOrEqual);
                    case ExpressionType.LessThan:
                        return Filter.Property(field, value, PropertyFilter.Types.Operator.LessThan);
                    case ExpressionType.LessThanOrEqual:
                        return Filter.Property(field, value, PropertyFilter.Types.Operator.LessThanOrEqual);
                }
            }
            else if (expression.NodeType == ExpressionType.MemberAccess &&
                     ((MemberExpression)expression).Member.MemberType == MemberTypes.Property &&
                     (((PropertyInfo)((MemberExpression)expression).Member).PropertyType == typeof(bool) ||
                      ((PropertyInfo)((MemberExpression)expression).Member).PropertyType == typeof(bool?)))
            {
                // Same as memberAccess == true.
                var field = GetFieldReferencedInExpression(expression, modelExpression, referenceModel);
                return Filter.Property(field, true, PropertyFilter.Types.Operator.Equal);
            }
            else if (expression.NodeType == ExpressionType.Not &&
                     ((UnaryExpression)expression).Operand.NodeType == ExpressionType.MemberAccess &&
                     ((MemberExpression)((UnaryExpression)expression).Operand).Member.MemberType ==
                     MemberTypes.Property &&
                     (((PropertyInfo)((MemberExpression)((UnaryExpression)expression).Operand).Member).PropertyType ==
                      typeof(bool) ||
                      ((PropertyInfo)((MemberExpression)((UnaryExpression)expression).Operand).Member).PropertyType ==
                      typeof(bool?)))
            {
                // Same as memberAccess == false.
                var field = GetFieldReferencedInExpression((MemberExpression)((UnaryExpression)expression).Operand, modelExpression, referenceModel);
                return Filter.Property(field, false, PropertyFilter.Types.Operator.Equal);
            }

            throw new InvalidOperationException($"Expression of type '{expression.NodeType}' is not supported in QueryAsync calls.");
        }

        public IEnumerable<PropertyOrder>? ConvertExpressionToOrder<T>(Expression expression, ParameterExpression modelExpression, T referenceModel, ref GeoQueryParameters<T>? geoParameters) where T : IModel
        {
            if (expression.NodeType == ExpressionType.Or)
            {
                if (geoParameters != null)
                {
                    throw new InvalidOperationException("Geographic queries can only sort by the geographic field at the top level; you can not order by multiple properties in a geographic query.");
                }

                var binaryExpression = (BinaryExpression)expression;
                return
                    ConvertExpressionToOrder(binaryExpression.Left, modelExpression, referenceModel, ref geoParameters)!.Concat(
                    ConvertExpressionToOrder(binaryExpression.Right, modelExpression, referenceModel, ref geoParameters)!);
            }
            else if (expression.NodeType == ExpressionType.Call)
            {
                var callExpression = (MethodCallExpression)expression;

                if (callExpression.Object != null ||
                    callExpression.Arguments.Count != 1)
                {
                    // Not an extension method call.
                    throw new InvalidOperationException($"You can only call supported extension methods on specific field types in queries; invoke unsupported '{callExpression.Method.Name}' method.");
                }

                var targetExpression = callExpression.Arguments[0];

                if (targetExpression.NodeType != ExpressionType.MemberAccess)
                {
                    throw new InvalidOperationException($"The only supported method call expressions must be made directly on a model's property. Attempted to invoke a method call on expression with type '{targetExpression.NodeType}'.");
                }

                var targetMemberAccess = (MemberExpression)targetExpression;
                if (targetMemberAccess.Expression != modelExpression ||
                    !(targetMemberAccess.Member is PropertyInfo))
                {
                    throw new InvalidOperationException($"The only supported method call expressions must be made directly on a model's property. Attempted to use member access on base expression of type '{targetMemberAccess.Expression?.NodeType}'.");
                }

                var propertyInfo = (PropertyInfo)targetMemberAccess.Member;

                if (callExpression.Method == typeof(GeoExtensions).GetMethod(nameof(GeoExtensions.Nearest), BindingFlags.Static | BindingFlags.Public) ||
                    callExpression.Method == typeof(GeoExtensions).GetMethod(nameof(GeoExtensions.Furthest), BindingFlags.Static | BindingFlags.Public))
                {
                    if (propertyInfo.PropertyType != typeof(LatLng))
                    {
                        throw new InvalidOperationException($"You can only use 'Nearest' and 'Furthest' on geopoint properties in sort expressions. Attempted to use member access on property named '{propertyInfo.Name}'.");
                    }

                    if (propertyInfo.Name != geoParameters?.GeoFieldName)
                    {
                        throw new InvalidOperationException($"You can only sort by geographic properties if you are also filtering on them with 'WithinKilometers'.");
                    }
                    else if (geoParameters.SortDirection.HasValue)
                    {
                        throw new InvalidOperationException($"You can only specify a geographic field once in a sort expression.");
                    }

                    geoParameters.SortDirection = callExpression.Method == typeof(GeoExtensions).GetMethod(nameof(GeoExtensions.Nearest), BindingFlags.Static | BindingFlags.Public) ? PropertyOrder.Types.Direction.Ascending : PropertyOrder.Types.Direction.Descending;
                    return null;
                }
                else
                {
                    throw new InvalidOperationException($"The only supported method call expressions are calling 'Nearest' or 'Furthest' on geopoint properties in sort expressions. Attempted to invoke unsupported '{callExpression.Method.Name}' method.");
                }
            }
            else if (expression.NodeType == ExpressionType.LessThan ||
                     expression.NodeType == ExpressionType.GreaterThan)
            {
                var leftField = GetFieldReferencedInExpression(((BinaryExpression)expression).Left, modelExpression, referenceModel);
                var rightField = GetFieldReferencedInExpression(((BinaryExpression)expression).Right, modelExpression, referenceModel);
                if (leftField != rightField)
                {
                    throw new InvalidOperationException($"Individual order expressions must be of the form 'x.prop > x.prop' or 'x.prop < x.prop', and the property name must be the same. The left field was '{leftField}' and the right field was '{rightField}'.");
                }

                return new PropertyOrder[1]
                {
                    new PropertyOrder
                    {
                        Property = new PropertyReference(leftField),
                        Direction = expression.NodeType == ExpressionType.LessThan ? PropertyOrder.Types.Direction.Ascending : PropertyOrder.Types.Direction.Descending,
                    }
                };
            }
            else
            {
                throw new InvalidOperationException($"Overall order expressions must be of the form 'x.prop > x.prop | x.prop2 < x.prop2'. The expression type was '{expression.NodeType}'.");
            }
        }

        private class SimplifyFilterStackValue
        {
            public CompositeFilter _filter;
            public int _index;
            public SimplifyFilterStackValue(CompositeFilter filter, int index)
            {
                _filter = filter;
                _index = index;
            }
        }

        public Filter? SimplifyFilter(Filter? filter)
        {
            if (filter == null)
            {
                return null;
            }

            if (filter.FilterTypeCase == Filter.FilterTypeOneofCase.PropertyFilter)
            {
                return filter;
            }

            // Otherwise, recursively expand all of the composite filters so we can have
            // a single top-level composite filter.
            var newFilters = new List<Filter>();
            var filterStack = new Stack<SimplifyFilterStackValue>();
            filterStack.Push(new SimplifyFilterStackValue(filter.CompositeFilter, 0));
            while (filterStack.Count > 0)
            {
                var stackValue = filterStack.Peek();
                if (stackValue._index >= stackValue._filter.Filters.Count)
                {
                    filterStack.Pop();
                    continue;
                }

                var nextFilter = stackValue._filter.Filters[stackValue._index];
                stackValue._index++;

                if (nextFilter.FilterTypeCase == Filter.FilterTypeOneofCase.PropertyFilter)
                {
                    newFilters.Add(nextFilter);
                }
                else
                {
                    filterStack.Push(new SimplifyFilterStackValue(nextFilter.CompositeFilter, 0));
                }
            }

            return Filter.And(newFilters);
        }
    }
}
