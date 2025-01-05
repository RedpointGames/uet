namespace Redpoint.CloudFramework.Repository.Converters.Expression
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using System.Collections.Generic;
    using System.Linq.Expressions;

    internal interface IExpressionConverter
    {
        Filter? ConvertExpressionToFilter<T>(Expression expression, ParameterExpression modelExpression, T referenceModel, ref GeoQueryParameters<T>? geoParameters, ref bool hasAncestorQuery) where T : Model;

        IEnumerable<PropertyOrder>? ConvertExpressionToOrder<T>(Expression expression, ParameterExpression modelExpression, T referenceModel, ref GeoQueryParameters<T>? geoParameters) where T : Model;

        Filter? SimplifyFilter(Filter? filter);
    }
}
