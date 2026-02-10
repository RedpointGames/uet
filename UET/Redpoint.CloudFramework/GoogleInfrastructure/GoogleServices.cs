namespace Redpoint.CloudFramework.GoogleInfrastructure
{
    using Google.Api.Gax.Grpc;
    using Google.Apis.Auth.OAuth2;
    using Google.Cloud.Datastore.V1;
    using Google.Cloud.PubSub.V1;
    using Grpc.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Redpoint.CloudFramework.Startup;
    using Redpoint.CloudFramework.Tracing;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    public class GoogleServices : IGoogleServices
    {
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IOptionalHelmConfiguration? _optionalHelmConfiguration;
        private readonly IManagedTracer _managedTracer;

        public GoogleServices(
            IHostEnvironment hostEnvironment,
            IManagedTracer managedTracer,
            IServiceProvider serviceProvider,
            IOptionalHelmConfiguration? optionalHelmConfiguration = null)
        {
            if (hostEnvironment.IsDevelopment() || hostEnvironment.IsStaging())
            {
                ProjectId = "local-dev";
            }
            else
            {
                var projectIdProvider = serviceProvider.GetService<IGoogleProjectIdProvider>();
                if (projectIdProvider != null)
                {
                    ProjectId = projectIdProvider.ProjectId;
                }
                else
                {
                    var gcProjectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT_ID");
                    if (!string.IsNullOrWhiteSpace(gcProjectId))
                    {
                        ProjectId = gcProjectId;
                    }
                    else
                    {
                        var filePath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
                        if (string.IsNullOrWhiteSpace(filePath))
                        {
                            throw new InvalidOperationException("GOOGLE_APPLICATION_CREDENTIALS is not set, and this application is not running in Development.");
                        }
                        var credentialText = File.ReadAllText(filePath);
                        if (string.IsNullOrWhiteSpace(credentialText))
                        {
                            throw new InvalidOperationException($"GOOGLE_APPLICATION_CREDENTIALS at path '{filePath}' is empty, and this application is not running in Development.");
                        }
                        var content = JsonObject.Parse(credentialText)!;

                        if (content["project_id"] == null)
                        {
                            throw new InvalidOperationException("GOOGLE_APPLICATION_CREDENTIALS is missing the project_id value!");
                        }

                        ProjectId = content["project_id"]!.ToString();
                    }
                }
            }
            _hostEnvironment = hostEnvironment;
            _optionalHelmConfiguration = optionalHelmConfiguration;
            _managedTracer = managedTracer;
        }

        public string ProjectId { get; set; }

        [SuppressMessage("Trimming", "IL2090:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The generic parameter of the source method or type does not have matching annotations.", Justification = "This method call is not trimmed.")]
        public TType Build<TType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.NonPublicMethods)] TBuilder>(string endpoint, IEnumerable<string> scopes) where TBuilder : ClientBuilderBase<TType>, new()
        {
            var builder = new TBuilder();
            builder.ChannelCredentials = GetChannelCredentials(endpoint, scopes);
            builder.Endpoint = GetServiceEndpoint(endpoint, scopes);
            var callInvoker = (CallInvoker)typeof(TBuilder)
                .GetMethod(
                    "CreateCallInvoker",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.DoNotWrapExceptions)!
                .Invoke(builder, null)!;
            builder.CallInvoker = new TracingCallInvoker(callInvoker, _managedTracer);
            // We have to set these back to defaults, because we've now instantiated the call invoker which used them.
            builder.ChannelCredentials = null;
            builder.Endpoint = null;
            return builder.Build();
        }

        public TType BuildRest<TType, TBuilder>(IEnumerable<string> scopes) where TBuilder : global::Google.Api.Gax.Rest.ClientBuilderBase<TType>, new()
        {
            var filePath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new InvalidOperationException("BuildRest not supported without GOOGLE_APPLICATION_CREDENTIALS being specified.");
            }

            using (var reader = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var googleCredentials = CredentialFactory.FromStream<GoogleCredential>(reader);
                if (googleCredentials.IsCreateScopedRequired)
                {
                    googleCredentials = googleCredentials.CreateScoped(scopes);
                }

                var builder = new TBuilder();
                builder.Credential = googleCredentials;
                // @todo: Figure this out.
                // builder.HttpClientFactory = new HttpClientFromMessageHandlerFactory(_httpClientFactory.CreateClient);
                return builder.Build();
            }
        }

        public ChannelCredentials? GetChannelCredentials(string endpoint, IEnumerable<string> scopes)
        {
            if (_hostEnvironment.IsDevelopment() || _hostEnvironment.IsStaging())
            {
                return ChannelCredentials.Insecure;
            }

            return null;
        }

        public string? GetServiceEndpoint(string endpoint, IEnumerable<string> scopes)
        {
            if (_hostEnvironment.IsDevelopment() || _hostEnvironment.IsStaging())
            {
                if (endpoint == PublisherServiceApiClient.DefaultEndpoint ||
                    endpoint == SubscriberServiceApiClient.DefaultEndpoint)
                {
                    var pubsubServerEnv = Environment.GetEnvironmentVariable("PUBSUB_SERVER");
                    if (!string.IsNullOrWhiteSpace(pubsubServerEnv))
                    {
                        return pubsubServerEnv;
                    }

                    var helmConfig = _optionalHelmConfiguration?.GetHelmConfig();
                    if (helmConfig != null)
                    {
                        return "localhost:" + helmConfig.PubSubPort;
                    }

                    if (Environment.GetEnvironmentVariable("GITLAB_CI") == "true")
                    {
                        return "pubsub:9000";
                    }

                    return "localhost:9000";
                }
                else if (endpoint == DatastoreClient.DefaultEndpoint)
                {
                    var datastoreServerEnv = Environment.GetEnvironmentVariable("DATASTORE_SERVER");
                    if (!string.IsNullOrWhiteSpace(datastoreServerEnv))
                    {
                        return datastoreServerEnv;
                    }

                    var helmConfig = _optionalHelmConfiguration?.GetHelmConfig();
                    if (helmConfig != null)
                    {
                        return "localhost:" + helmConfig.DatastorePort;
                    }

                    if (Environment.GetEnvironmentVariable("GITLAB_CI") == "true")
                    {
                        return "datastore:9001";
                    }

                    return "localhost:9001";
                }

                throw new InvalidOperationException($"The service at {endpoint} is not supported in the local development environment.");
            }

            return null;
        }
    }
}
