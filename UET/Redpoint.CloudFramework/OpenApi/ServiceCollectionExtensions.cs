namespace Redpoint.CloudFramework.OpenApi
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ApplicationModels;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using Microsoft.OpenApi.Models;
    using NodaTime;
    using System;
    using System.IO;
    using System.Reflection;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Unicode;

    public static class ServiceCollectionExtensions
    {
        private class JsonOptionsWasConfiguredForSwaggerReactApp
        {
        }

        public static IMvcBuilder AddJsonOptionsForSwaggerReactApp(this IMvcBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Services.AddSingleton<JsonOptionsWasConfiguredForSwaggerReactApp>();
            return builder.AddJsonOptions(options =>
            {
                var encoderSettings = new TextEncoderSettings();
                encoderSettings.AllowRange(UnicodeRanges.BasicLatin);
                encoderSettings.ForbidCharacters('<', '>', '&', '\'', '"');
                options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(encoderSettings);
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.Converters.Add(new InstantJsonConverter());
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            });
        }

        public static void AddSwaggerGenForReactApp(this IServiceCollection services, string productName = "Internal API")
        {
            services.AddTransient<IApplicationModelProvider, RedpointApplicationModelProvider>();

            var jsonOptionsWasConfigured = services.FirstOrDefault(x => x.ServiceType == typeof(JsonOptionsWasConfiguredForSwaggerReactApp));
            if (jsonOptionsWasConfigured == null)
            {
                throw new JsonOptionsNotConfiguredException();
            }

            services.AddSwaggerGen(options =>
            {
                options.DocumentFilter<OnlyApiMethodsFilter>();
                options.OperationFilter<FormFileOperationFilter>();
                options.SchemaFilter<RequiredSchemaFilter>();
                options.SchemaFilter<PaginatedQueryCursorSchemaFilter>();
                options.SchemaFilter<ExcludeSchemaFilter>();
                options.DocumentFilter<ExcludeSchemaDocumentFilter>();
                options.MapType<Instant>(() =>
                {
                    return new OpenApiSchema
                    {
                        Type = "string",
                        Format = "date-time",
                    };
                });

                options.CustomSchemaIds(x =>
                {
                    if (x.IsConstructedGenericType &&
                        x.GetGenericTypeDefinition() == typeof(Errorable<>))
                    {
                        return "Errorable" + x.GetGenericArguments()[0].Name;
                    }
                    else if (x.IsConstructedGenericType)
                    {
                        var genericName = x.GetGenericTypeDefinition().Name;
                        return string.Concat(genericName.AsSpan(0, x.GetGenericTypeDefinition().Name.IndexOf('`', StringComparison.Ordinal)), "_", x.GetGenericArguments()[0].Name);
                    }
                    else
                    {
                        return x.Name;
                    }
                });

                options.SupportNonNullableReferenceTypes();
                options.UseAllOfToExtendReferenceSchemas();
                options.UseAllOfForInheritance();

                options.CustomOperationIds(e => $"{e.ActionDescriptor.RouteValues["action"]}");

                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "1.0.0",
                    Title = productName,
                    Description = $"Describes the API endpoints of {productName}, which are used by the frontend React components. Not for public use."
                });

                options.DocInclusionPredicate((docName, description) =>
                {
                    return description.RelativePath != null && description.RelativePath.StartsWith("api/", StringComparison.Ordinal);
                });

                var xmlFilename = $"{Assembly.GetEntryAssembly()!.GetName().Name}.xml";
                if (System.IO.File.Exists(Path.Combine(AppContext.BaseDirectory, xmlFilename)))
                {
                    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
                }
                else
                {
#pragma warning disable IL3000 // Avoid accessing Assembly file path when publishing as a single file
                    var assemblyLocation = Assembly.GetEntryAssembly()!.Location;
#pragma warning restore IL3000 // Avoid accessing Assembly file path when publishing as a single file
                    if (!string.IsNullOrEmpty(assemblyLocation))
                    {
                        var altXmlFilename = Path.Combine(Path.GetDirectoryName(assemblyLocation)!, xmlFilename);
                        if (System.IO.File.Exists(altXmlFilename))
                        {
                            options.IncludeXmlComments(altXmlFilename);
                        }
                    }
                }
            });
        }
    }
}
