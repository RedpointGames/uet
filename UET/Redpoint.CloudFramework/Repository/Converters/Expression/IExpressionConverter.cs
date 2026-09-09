namespace Redpoint.CloudFramework.Repository.Converters.Expression
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using System.Collections.Generic;
    using System.Linq.Expressions;

    internal interface IExpressionConverter
    {
        Filter? ConvertExpressionToFilter<T>(Expression expression, ParameterExpression modelExpression, IReferenceModel<T> referenceModel, ref GeoQueryParameters<T>? geoParameters, ref bool hasAncestorQuery) where T : class, IModel, new();

        IEnumerable<PropertyOrder>? ConvertExpressionToOrder<T>(Expression expression, ParameterExpression modelExpression, IReferenceModel<T> referenceModel, ref GeoQueryParameters<T>? geoParameters) where T : class, IModel, new();

        Filter? SimplifyFilter(Filter? filter);

        string RenderFilterToString(Filter? filter);

        string RenderOrderToString(IEnumerable<PropertyOrder>? order);

        string RenderQueryToString(Query query);

        string RenderQueryToString(string kind, Filter? filter, IReadOnlyList<PropertyOrder>? order);
    }
}
