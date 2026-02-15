namespace Redpoint.CloudFramework.Tests.React
{
    using global::React;
    using global::React.AspNet;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ApplicationParts;
    using Microsoft.AspNetCore.Mvc.Controllers;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.OpenApi;
    using React;
    using Redpoint.CloudFramework.OpenApi;
    using Swashbuckle.AspNetCore.Swagger;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Xunit;

    public enum TestEnum
    {
        Test,
        AnotherCamelCase,
    }

    public class TestClass
    {
        public TestEnum EnumCamelCase { get; set; }
    }

    public class TestController : Controller
    {
        [HttpGet, Api, Route("/api/v1/test")]
        public TestClass Test()
        {
            return new TestClass
            {
                EnumCamelCase = TestEnum.AnotherCamelCase
            };
        }
    }

    public class ReactTests
    {
        [Fact]
        public void TestJsonEncoding()
        {
            var builder = WebApplication.CreateBuilder([]);

            builder.Services.AddLogging();
            builder.Services.AddControllersWithViews()
                .AddControllersAsServices()
                .AddJsonOptionsForSwaggerReactApp();

            builder.Services.AddReact();
            builder.Services.AddSwaggerGenForReactApp();

            var app = builder.Build();
            app.UseRouting();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            var sp = app.Services;
            var siteConfiguration = sp.GetRequiredService<IReactSiteConfiguration>();

            var encoded = JsonSerializer.Serialize(
                new TestClass
                {
                    EnumCamelCase = TestEnum.AnotherCamelCase,
                },
                siteConfiguration.JsonSerializerSettings);
            Assert.Equal(
                @"{""enumCamelCase"":""anotherCamelCase""}",
                encoded);
        }

        private class TestWebHostEnvironment : IWebHostEnvironment
        {
            public string WebRootPath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public IFileProvider WebRootFileProvider { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public string ApplicationName { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public IFileProvider ContentRootFileProvider { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public string ContentRootPath { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public string EnvironmentName { get => "Development"; set => throw new NotImplementedException(); }
        }

        [Fact]
        public void TestOpenApiGeneration()
        {
            var builder = WebApplication.CreateBuilder([]);

            builder.Services.AddLogging();
            builder.Services.AddControllersWithViews()
                .AddControllersAsServices()
                .AddJsonOptionsForSwaggerReactApp();

            builder.Services.AddReact();
            builder.Services.AddSwaggerGenForReactApp();

            var app = builder.Build();
            app.UseRouting();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            var sp = app.Services;
            var swaggerProvider = sp.GetRequiredService<ISwaggerProvider>();

            _ = sp.GetRequiredService<TestController>();

            var partManager = sp.GetRequiredService<ApplicationPartManager>();
            var applicationParts = partManager.ApplicationParts.Select(x => x.Name);
            var controllerFeature = new ControllerFeature();
            partManager.PopulateFeature(controllerFeature);
            var controllers = controllerFeature.Controllers.Select(x => x.Name);
            Console.WriteLine($"Found the following application parts: '{string.Join(", ", applicationParts)}' with the following controllers: '{string.Join(", ", controllers)}'");

            var endpointSources = sp.GetServices<EndpointDataSource>().ToList();
            Console.WriteLine($"Found {endpointSources.Count} endpoint sources.");
            foreach (var endpointSource in endpointSources)
            {
                Console.WriteLine($"  {endpointSource.Endpoints.Count} endpoints:");
                foreach (var endpoint in endpointSource.Endpoints.OfType<RouteEndpoint>())
                {
                    Console.WriteLine($"    /{endpoint.RoutePattern.RawText?.TrimStart('/')}");
                }
            }

            var swagger = swaggerProvider.GetSwagger(
                documentName: "v1",
                host: null,
                basePath: null);
            Assert.NotNull(swagger);

            using (var textWriter = new StringWriter(CultureInfo.InvariantCulture))
            {
                var jsonWriter = new OpenApiJsonWriter(textWriter);

                swagger.SerializeAsV3(jsonWriter);

                Console.WriteLine(textWriter.ToString());
            }

            Assert.NotEmpty(swagger.Paths);
            var testPath = Assert.Contains("/api/v1/test", swagger.Paths);
            var testGetPath = Assert.Contains(HttpMethod.Get, testPath.Operations!);
            Assert.Equal("Test", testGetPath.OperationId);

            Assert.NotEmpty(swagger.Components!.Schemas!);

            var testEnumSchema = Assert.Contains("TestEnum", swagger.Components.Schemas!);
            Assert.NotEmpty(testEnumSchema.Enum!);
            Assert.All(testEnumSchema.Enum!, x => Assert.Equal(JsonValueKind.String, x.GetValueKind()));

            var enumValues = testEnumSchema.Enum!.Select(x => x.GetValue<string>()).ToList();
            Assert.Equal(2, enumValues.Count);
            Assert.Equal("test", enumValues[0]);
            Assert.Equal("anotherCamelCase", enumValues[1]);
        }
    }
}
