namespace Redpoint.CloudFramework.Tests.Converters
{
    using Google.Cloud.Datastore.V1;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;
    using Google.Type;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Converters.Value;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using Redpoint.CloudFramework.Tests.Models;
    using Redpoint.CloudFramework.Tracing;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Xunit;
    using static Google.Cloud.Datastore.V1.Value;
    using Value = Google.Cloud.Datastore.V1.Value;

    public class ConverterTests
    {
        private DatastoreValueConvertToContext _datastoreToContext = new DatastoreValueConvertToContext
        {
            ModelNamespace = string.Empty,
            Model = new TestModel(),
            Entity = new Entity(),
        };
        private DatastoreValueConvertFromContext _datastoreFromContext = new DatastoreValueConvertFromContext
        {
            ModelNamespace = string.Empty,
        };
        private JsonValueConvertToContext _jsonToContext = new JsonValueConvertToContext
        {
            ModelNamespace = string.Empty,
            Model = new TestModel(),
        };
        private JsonValueConvertFromContext _jsonFromContext = new JsonValueConvertFromContext
        {
            ModelNamespace = string.Empty,
        };

        private T GetConverter<T>() where T : IValueConverter
        {
            var services = new ServiceCollection();
            services.AddSingleton<IHostEnvironment>(_ => new TestHostEnvironment());
            services.AddSingleton<IManagedTracer, NullManagedTracer>();
            services.AddCloudFrameworkCore();
            services.AddCloudFrameworkGoogleCloud();
            var converters = services
                .BuildServiceProvider()
                .GetServices<IValueConverter>();
            Assert.NotEmpty(converters);
            return (T)converters.First(x => x is T);
        }

        private void AssertConversion<TClr>(
            IValueConverter converter,
            Action<Value> expectedDatastoreValue,
            FieldType expectedFieldType,
            string expectedJson,
            TClr clrValue)
        {
            Assert.Equal(
                expectedFieldType,
                converter.GetFieldType());
            Assert.True(
                converter.IsConverterForClrType(typeof(TClr)));

            var datastoreValue = converter.ConvertToDatastoreValue(
                _datastoreToContext,
                "field",
                typeof(TClr),
                clrValue,
                true);
            var jsonValue = converter.ConvertToJsonToken(
                _jsonToContext,
                "field",
                typeof(TClr),
                clrValue!);
            Assert.NotNull(datastoreValue);
            Assert.NotNull(jsonValue);

            expectedDatastoreValue(datastoreValue);
            var actualJson = JsonSerializer.Serialize(jsonValue);
            Assert.Equal(expectedJson, actualJson);

            var restoredValueFromDatastore = converter.ConvertFromDatastoreValue(
                _datastoreFromContext,
                "field",
                typeof(TClr),
                datastoreValue,
                _ => { });
            var restoredValueFromJson = converter.ConvertFromJsonToken(
                _jsonFromContext,
                "field",
                typeof(TClr),
                jsonValue,
                _ => { });

            Assert.Equal(clrValue, restoredValueFromDatastore);
            Assert.Equal(clrValue, restoredValueFromJson);
        }

        [Fact]
        public void Boolean()
        {
            var converter = GetConverter<BooleanValueConverter>();

            AssertConversion(
                converter,
                value =>
                {
                    Assert.True(value.BooleanValue);
                },
                FieldType.Boolean,
                "true",
                true);
            AssertConversion(
                converter,
                value =>
                {
                    Assert.False(value.BooleanValue);
                },
                FieldType.Boolean,
                "false",
                false);
        }

        [Fact]
        public void String()
        {
            var converter = GetConverter<StringValueConverter>();

            AssertConversion(
                converter,
                value =>
                {
                    Assert.Equal("hello world", value.StringValue);
                },
                FieldType.String,
                "\"hello world\"",
                "hello world");
        }

        [Fact]
        public void Integer()
        {
            var converter = GetConverter<IntegerValueConverter>();

            AssertConversion(
                converter,
                value =>
                {
                    Assert.Equal(2L, value.IntegerValue);
                },
                FieldType.Integer,
                "2",
                2L);
        }

        [Fact]
        public void UnsignedInteger()
        {
            var converter = GetConverter<UnsignedIntegerValueConverter>();

            AssertConversion(
                converter,
                value =>
                {
                    Assert.Equal(2L, value.IntegerValue);
                },
                FieldType.UnsignedInteger,
                "2",
                2UL);
        }

        [Fact]
        public void Double()
        {
            var converter = GetConverter<DoubleValueConverter>();

            AssertConversion(
                converter,
                value =>
                {
                    Assert.Equal(2.5, value.DoubleValue);
                },
                FieldType.Double,
                "2.5",
                2.5);
        }

        [Fact]
        public void UnsafeKey()
        {
            var converter = GetConverter<UnsafeKeyValueConverter>();

            var key = new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 1)
                }
            };

            AssertConversion(
                converter,
                value =>
                {
                    Assert.Equal(key, value.KeyValue);
                },
                FieldType.UnsafeKey,
                "\"#v1|project||key:id=1\"",
                key);
        }

        [Fact]
        public void StringArray()
        {
            var converter = GetConverter<StringArrayValueConverter>();

            AssertConversion(
                converter,
                value =>
                {
                    Assert.Equal(3, value.ArrayValue.Values.Count);
                    Assert.Equal("hello1", value.ArrayValue.Values[0].StringValue);
                    Assert.Equal("hello2", value.ArrayValue.Values[1].StringValue);
                    Assert.Equal("hello3", value.ArrayValue.Values[2].StringValue);
                },
                FieldType.StringArray,
                "[\"hello1\",\"hello2\",\"hello3\"]",
                new[]
                {
                    "hello1",
                    "hello2",
                    "hello3"
                });
        }

        [Fact]
        public void UnsignedIntegerArray()
        {
            var converter = GetConverter<UnsignedIntegerArrayValueConverter>();

            AssertConversion(
                converter,
                value =>
                {
                    Assert.Equal(3, value.ArrayValue.Values.Count);
                    Assert.Equal(1L, value.ArrayValue.Values[0].IntegerValue);
                    Assert.Equal(2L, value.ArrayValue.Values[1].IntegerValue);
                    Assert.Equal(3L, value.ArrayValue.Values[2].IntegerValue);
                },
                FieldType.UnsignedIntegerArray,
                "[1,2,3]",
                new[]
                {
                    1UL,
                    2UL,
                    3UL
                });
        }

        private Entity CreateEntity(bool nested = false)
        {
            var key = new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 1)
                }
            };

            var entity = new Entity
            {
                Properties =
                {
                    { "string", new Value { StringValue = "test" } },
                    { "integer", new Value { IntegerValue = 42 } },
                    { "double", new Value { DoubleValue = 2.5 } },
                    { "bool", new Value { BooleanValue = true } },
                    { "blob", new Value { BlobValue = ByteString.CopyFrom([1, 2, 3]) } },
                    { "geopoint", new Value { GeoPointValue = new LatLng { Latitude = 100, Longitude = 50 } } },
                    { "key", new Value { KeyValue = key } },
                    { "timestamp", new Value { TimestampValue = Timestamp.FromDateTimeOffset(DateTimeOffset.MinValue) } },
                    { 
                        "array", 
                        new Value 
                        { 
                            ArrayValue = new ArrayValue
                            {
                                Values =
                                {
                                    new Value { StringValue = "test" },
                                    new Value { IntegerValue = 42 },
                                    new Value { DoubleValue = 2.5 },
                                    new Value { BooleanValue = true },
                                    new Value { BlobValue = ByteString.CopyFrom([1, 2, 3]) },
                                    new Value { GeoPointValue = new LatLng { Latitude = 100, Longitude = 50 } },
                                    new Value { KeyValue = key },
                                    new Value { TimestampValue = Timestamp.FromDateTimeOffset(DateTimeOffset.MinValue) },
                                }
                            } 
                        } 
                    },
                }
            };
            if (!nested)
            {
                entity.Properties.Add("entity", CreateEntity(true));
            }

            return entity;
        }

        [Fact]
        public void EmbeddedEntity()
        {
            var converter = GetConverter<EmbeddedEntityValueConverter>();

            var entity = CreateEntity();

            AssertConversion(
                converter,
                value =>
                {
                    Assert.Equal(entity, value.EntityValue);
                },
                FieldType.EmbeddedEntity,
                @"{""string"":{""type"":""string"",""value"":""test""},""integer"":{""type"":""integer"",""value"":42},""double"":{""type"":""double"",""value"":2.5},""bool"":{""type"":""boolean"",""value"":true},""blob"":{""type"":""blob"",""value"":""AQID""},""geopoint"":{""type"":""geopoint"",""value"":{""latitude"":100,""longitude"":50}},""key"":{""type"":""key"",""value"":{""ns"":"""",""value"":""#v1|project||key:id=1""}},""timestamp"":{""type"":""timestamp"",""value"":{""seconds"":-62135596800,""nanos"":0}},""array"":[{""type"":""string"",""value"":""test""},{""type"":""integer"",""value"":42},{""type"":""double"",""value"":2.5},{""type"":""boolean"",""value"":true},{""type"":""blob"",""value"":""AQID""},{""type"":""geopoint"",""value"":{""latitude"":100,""longitude"":50}},{""type"":""key"",""value"":{""ns"":"""",""value"":""#v1|project||key:id=1""}},{""type"":""timestamp"",""value"":{""seconds"":-62135596800,""nanos"":0}}],""entity"":{""type"":""entity"",""value"":{""string"":{""type"":""string"",""value"":""test""},""integer"":{""type"":""integer"",""value"":42},""double"":{""type"":""double"",""value"":2.5},""bool"":{""type"":""boolean"",""value"":true},""blob"":{""type"":""blob"",""value"":""AQID""},""geopoint"":{""type"":""geopoint"",""value"":{""latitude"":100,""longitude"":50}},""key"":{""type"":""key"",""value"":{""ns"":"""",""value"":""#v1|project||key:id=1""}},""timestamp"":{""type"":""timestamp"",""value"":{""seconds"":-62135596800,""nanos"":0}},""array"":[{""type"":""string"",""value"":""test""},{""type"":""integer"",""value"":42},{""type"":""double"",""value"":2.5},{""type"":""boolean"",""value"":true},{""type"":""blob"",""value"":""AQID""},{""type"":""geopoint"",""value"":{""latitude"":100,""longitude"":50}},{""type"":""key"",""value"":{""ns"":"""",""value"":""#v1|project||key:id=1""}},{""type"":""timestamp"",""value"":{""seconds"":-62135596800,""nanos"":0}}]}}}",
                entity);
        }

        [Fact]
        public void StringEnum()
        {
            var converter = GetConverter<StringEnumValueConverter>();

            AssertConversion(
                converter,
                value =>
                {
                    Assert.Equal("a", value.StringValue);
                },
                FieldType.String,
                "\"a\"",
                TestStringEnum.A);
        }

        [Fact]
        public void StringEnumArray()
        {
            var converter = GetConverter<StringEnumArrayValueConverter>();

            AssertConversion(
                converter,
                value =>
                {
                    Assert.Equal(3, value.ArrayValue.Values.Count);
                    Assert.Equal("a", value.ArrayValue.Values[0].StringValue);
                    Assert.Equal("b", value.ArrayValue.Values[1].StringValue);
                    Assert.Equal("c", value.ArrayValue.Values[2].StringValue);
                },
                FieldType.StringArray,
                "[\"a\",\"b\",\"c\"]",
                new[]
                {
                    TestStringEnum.A,
                    TestStringEnum.B,
                    TestStringEnum.C
                });
        }

        [Fact]
        public void StringEnumSet()
        {
            var converter = GetConverter<StringEnumSetValueConverter>();

            AssertConversion(
                converter,
                value =>
                {
                    Assert.Equal(3, value.ArrayValue.Values.Count);
                    Assert.Equal("a", value.ArrayValue.Values[0].StringValue);
                    Assert.Equal("b", value.ArrayValue.Values[1].StringValue);
                    Assert.Equal("c", value.ArrayValue.Values[2].StringValue);
                },
                FieldType.StringArray,
                "[\"a\",\"b\",\"c\"]",
                new[]
                {
                    TestStringEnum.A,
                    TestStringEnum.B,
                    TestStringEnum.C
                });
        }
    }
}
