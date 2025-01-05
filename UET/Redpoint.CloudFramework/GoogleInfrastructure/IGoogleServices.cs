namespace Redpoint.CloudFramework.GoogleInfrastructure
{
    using Google.Api.Gax.Grpc;
    using Grpc.Core;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    public interface IGoogleServices
    {
        string ProjectId { get; }

        TType Build<TType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicMethods)] TBuilder>(string endpoint, IEnumerable<string> scopes) where TBuilder : ClientBuilderBase<TType>, new();

        TType BuildRest<TType, TBuilder>(IEnumerable<string> scopes) where TBuilder : global::Google.Api.Gax.Rest.ClientBuilderBase<TType>, new();

        ChannelCredentials? GetChannelCredentials(string endpoint, IEnumerable<string> scopes);

        string? GetServiceEndpoint(string endpoint, IEnumerable<string> scopes);
    }
}
