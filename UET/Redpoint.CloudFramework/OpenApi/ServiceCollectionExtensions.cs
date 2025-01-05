namespace Redpoint.CloudFramework.OpenApi
{
    using Microsoft.AspNetCore.Mvc.ApplicationModels;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.OpenApi.Models;
    using NodaTime;
    using System;
    using System.IO;
    using System.Reflection;

    public static class ServiceCollectionExtensions
    {
        public static IMvcBuilder AddCloudFrameworkCustomisation(this IMvcBuilder builder)
        {
            builder.AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new InstantJsonConverter());
            });
            return builder;
        }

        public static void AddSwaggerGenForReactApp(this IServiceCollection services, string productName = "Internal API")
        {
            services.AddTransient<IApplicationModelProvider, RedpointApplicationModelProvider>();

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
