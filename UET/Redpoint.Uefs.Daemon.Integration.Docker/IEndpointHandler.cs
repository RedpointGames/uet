namespace Redpoint.Uefs.Daemon.Integration.Docker
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;

    public interface IEndpointHandler
    {
        ValueTask<(int, string)> HandleAsync(
            IUefsDaemon plugin, 
            string request,
            JsonSerializerContext jsonSerializerContext);
    }

    public interface IEndpointHandler<TRequest, TResponse> : IEndpointHandler where TRequest : notnull where TResponse : notnull
    {
        public abstract ValueTask<TResponse> HandleAsync(
            IUefsDaemon plugin, 
            TRequest request);

        async ValueTask<(int, string)> IEndpointHandler.HandleAsync(
            IUefsDaemon plugin, 
            string request,
            JsonSerializerContext jsonSerializerContext)
        {
            var requestTypeInfo = (JsonTypeInfo<TRequest>?)jsonSerializerContext.GetTypeInfo(typeof(TRequest));
            if (requestTypeInfo == null)
            {
                throw new InvalidOperationException($"Missing compile-time serialization info for {typeof(TRequest)}");
            }
            var responseTypeInfo = (JsonTypeInfo<TResponse>?)jsonSerializerContext.GetTypeInfo(typeof(TResponse))!;
            if (responseTypeInfo == null)
            {
                throw new InvalidOperationException($"Missing compile-time serialization info for {typeof(TResponse)}");
            }

            try
            {
                if (typeof(TRequest) == typeof(EmptyRequest))
                {
                    return (
                        200,
                        JsonSerializer.Serialize(
                            await HandleAsync(plugin, (TRequest)(object)new EmptyRequest()).ConfigureAwait(false),
                            responseTypeInfo)
                    );
                }
                else
                {
                    var requestObject = JsonSerializer.Deserialize(
                        request,
                        requestTypeInfo);
                    if (requestObject == null)
                    {
                        //plugin.Logger.LogError($"Unable to deserialize request for '{typeof(TRequest).Name}' with content: {request}");
                        return (400, "{\"Err\": \"Invalid request data.\"}");
                    }
                    return (
                        200, 
                        JsonSerializer.Serialize(
                            await HandleAsync(plugin, requestObject).ConfigureAwait(false),
                            responseTypeInfo)
                    );
                }
            }
            catch (EndpointException<TResponse> ex)
            {
                return (
                    ex.StatusCode, 
                    JsonSerializer.Serialize(
                        (TResponse)ex.ErrorResponse,
                        responseTypeInfo)
                );
            }
        }
    }
}
