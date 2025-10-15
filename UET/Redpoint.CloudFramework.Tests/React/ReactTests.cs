namespace Redpoint.CloudFramework.Tests.React
{
    using global::React;
    using global::React.AspNet;
    using Microsoft.Extensions.DependencyInjection;
    using React;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Xunit;

    public class ReactTests
    {
        public enum TestEnum
        {
            Test,
            AnotherCamelCase,
        }

        public class TestClass
        {
            public TestEnum EnumCamelCase { get; set; }
        }

        [Fact]
        public void TestJsonEncoding()
        {
            var services = new ServiceCollection();
            services.AddReact();

            var sp = services.BuildServiceProvider();
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
    }
}
