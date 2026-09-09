namespace Redpoint.CloudFramework.Tests.Converters
{
    using Google.Cloud.Datastore.V1;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;
    using Google.Type;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NodaTime;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Converters.Value;
    using Redpoint.CloudFramework.Repository.Converters.Value.Context;
    using Redpoint.CloudFramework.Tests.Models;
    using Redpoint.CloudFramework.Tracing;
    using Redpoint.StringEnum;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Xunit;
    using static Google.Cloud.Datastore.V1.Value;
    using Value = Google.Cloud.Datastore.V1.Value;

    public class ConverterTests
    {
        private class Contexts
        {
            public required DatastoreValueConvertToContext DatastoreToContext;
            public required DatastoreValueConvertFromContext DatastoreFromContext;
            public required JsonValueConvertToContext JsonToContext;
            public required JsonValueConvertFromContext JsonFromContext;
        }

        [Kind("key")]
        public sealed class ConverterTestModel : Model<ConverterTestModel>
        {
            [Type(FieldType.String), Indexed]
            public string? @namespace { get; set; }

            [Type(FieldType.LocalKey), Indexed]
            public Key<ConverterTestModel>? localKey { get; set; }

            public override string GetDatastoreNamespaceForLocalKeys()
            {
                return @namespace ?? string.Empty;
            }
        }

        private Contexts _global = new Contexts
        {
            DatastoreToContext = new DatastoreValueConvertToContext
            {
                ModelNamespace = string.Empty,
                Model = new ConverterTestModel
                {
                    Key = new Key<ConverterTestModel>(new Key
                    {
                        PartitionId = new PartitionId
                        {
                            ProjectId = "project",
                        },
                        Path =
                        {
                            new Key.Types.PathElement("key", 1)
                        }
                    }),
                    @namespace = "local",
                },
                ReferenceModel = ReferenceModelCache.Get<ConverterTestModel>(),
                Entity = new Entity
                {
                    Key = new Key
                    {
                        PartitionId = new PartitionId
                        {
                            ProjectId = "project",
                        },
                        Path =
                        {
                            new Key.Types.PathElement("key", 1)
                        }
                    }
                }
            },
            DatastoreFromContext = new DatastoreValueConvertFromContext
            {
                ModelNamespace = string.Empty,
            },
            JsonToContext = new JsonValueConvertToContext
            {
                ModelNamespace = string.Empty,
                Model = new ConverterTestModel
                {
                    Key = new Key<ConverterTestModel>(new Key
                    {
                        PartitionId = new PartitionId
                        {
                            ProjectId = "project",
                        },
                        Path =
                        {
                            new Key.Types.PathElement("key", 1)
                        }
                    }),
                    @namespace = "local",
                },
                ReferenceModel = ReferenceModelCache.Get<ConverterTestModel>(),
            },
            JsonFromContext = new JsonValueConvertFromContext
            {
                ModelNamespace = string.Empty,
            }
        };

        private Contexts _local = new Contexts
        {
            DatastoreToContext = new DatastoreValueConvertToContext
            {
                ModelNamespace = "local",
                Model = new ConverterTestModel
                {
                    Key = new Key<ConverterTestModel>(new Key
                    {
                        PartitionId = new PartitionId
                        {
                            ProjectId = "project",
                            NamespaceId = "local",
                        },
                        Path =
                        {
                            new Key.Types.PathElement("key", 1)
                        }
                    }),
                    @namespace = "local",
                },
                ReferenceModel = ReferenceModelCache.Get<ConverterTestModel>(),
                Entity = new Entity
                {
                    Key = new Key
                    {
                        PartitionId = new PartitionId
                        {
                            ProjectId = "project",
                            NamespaceId = "local",
                        },
                        Path =
                        {
                            new Key.Types.PathElement("key", 1)
                        }
                    }
                }
            },
            DatastoreFromContext = new DatastoreValueConvertFromContext
            {
                ModelNamespace = "local",
            },
            JsonToContext = new JsonValueConvertToContext
            {
                ModelNamespace = "local",
                Model = new ConverterTestModel
                {
                    Key = new Key<ConverterTestModel>(new Key
                    {
                        PartitionId = new PartitionId
                        {
                            ProjectId = "project",
                            NamespaceId = "local",
                        },
                        Path =
                        {
                            new Key.Types.PathElement("key", 1)
                        }
                    }),
                    @namespace = "local",
                },
                ReferenceModel = ReferenceModelCache.Get<ConverterTestModel>(),
            },
            JsonFromContext = new JsonValueConvertFromContext
            {
                ModelNamespace = "local",
            }
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

        private IValueConverter GetConverter(FieldType fieldType)
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
            return converters.First(x => x.GetFieldType() == fieldType);
        }

        private void AssertConversion<TClr>(
            Contexts contexts,
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
                contexts.DatastoreToContext,
                "field",
                typeof(TClr),
                clrValue,
                true);
            var jsonValue = converter.ConvertToJsonToken(
                contexts.JsonToContext,
                "field",
                typeof(TClr),
                clrValue!);
            Assert.NotNull(datastoreValue);
            Assert.NotNull(jsonValue);

            expectedDatastoreValue(datastoreValue);
            var actualJson = JsonSerializer.Serialize(jsonValue);
            Assert.Equal(expectedJson, actualJson);

            var callbacks = new List<ConvertFromDelayedLoad>();
            var restoredValueFromDatastore = converter.ConvertFromDatastoreValue(
                contexts.DatastoreFromContext,
                "field",
                typeof(TClr),
                datastoreValue,
                callbacks.Add);
            foreach (var callback in callbacks)
            {
                restoredValueFromDatastore = callback(contexts.DatastoreToContext.Model.GetDatastoreNamespaceForLocalKeys());
            }
            callbacks.Clear();
            var restoredValueFromJson = converter.ConvertFromJsonToken(
                contexts.JsonFromContext,
                "field",
                typeof(TClr),
                jsonValue,
                callbacks.Add);
            foreach (var callback in callbacks)
            {
                restoredValueFromJson = callback(contexts.DatastoreToContext.Model.GetDatastoreNamespaceForLocalKeys());
            }
            callbacks.Clear();

            Assert.Equal(clrValue, restoredValueFromDatastore);
            Assert.Equal(clrValue, restoredValueFromJson);

#pragma warning disable CS8625
            var jsonNullEx = Assert.Throws<JsonValueWasNullException>(() =>
            {
                converter.ConvertFromJsonToken(
                    contexts.JsonFromContext,
                    "field",
                    typeof(TClr),
                    null,
                    _ => { });
            });
            Assert.NotNull(jsonNullEx);
            Assert.Equal("field", jsonNullEx.PropertyName);
#pragma warning restore CS8625
        }

        [Fact]
        public void Boolean()
        {
            var converter = GetConverter<BooleanValueConverter>();

            AssertConversion(
                _global,
                converter,
                value =>
                {
                    Assert.True(value.BooleanValue);
                },
                FieldType.Boolean,
                "true",
                true);
            AssertConversion(
                _global,
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
                _global,
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
                _global,
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
                _global,
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
                _global,
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
            var converter = GetConverter(FieldType.UnsafeKey);

            var key = new UntypedKey(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 1)
                }
            });

            AssertConversion(
                _global,
                converter,
                value =>
                {
                    Assert.Equal(key, new UntypedKey(value.KeyValue));
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
                _global,
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
                _global,
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
                    { "timestamp", new Value { TimestampValue = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.MinValue) } },
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
                                    new Value { TimestampValue = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.MinValue) },
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
                _global,
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
                _global,
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
                _global,
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
                new StringEnumValue<TestStringEnum>[]
                {
                    TestStringEnum.A,
                    TestStringEnum.B,
                    TestStringEnum.C
                });
            AssertConversion(
                _global,
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
                new List<StringEnumValue<TestStringEnum>>
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
                _global,
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
                new HashSet<StringEnumValue<TestStringEnum>>
                {
                    TestStringEnum.A,
                    TestStringEnum.B,
                    TestStringEnum.C
                });
        }

        [Fact]
        public void Geopoint()
        {
            var converter = GetConverter<GeopointValueConverter>();

            var geopoint = new LatLng { Latitude = 100, Longitude = 50 };

            AssertConversion(
                _global,
                converter,
                value =>
                {
                    Assert.Equal(geopoint, value.GeoPointValue);
                },
                FieldType.Geopoint,
                "{\"lat\":100,\"long\":50}",
                geopoint);
        }

        [Fact]
        public void Timestamp()
        {
            var converter = GetConverter<TimestampValueConverter>();

            var instant = Instant.FromUnixTimeSeconds(100).PlusNanoseconds(50);

            AssertConversion<Instant?>(
                _global,
                converter,
                value =>
                {
                    Assert.Equal(100, value.TimestampValue.Seconds);
                    Assert.Equal(50, value.TimestampValue.Nanos);
                },
                FieldType.Timestamp,
                "{\"seconds\":100,\"nanos\":50}",
                instant);
        }

        private record class JsonTest
        {
            [JsonPropertyName("test")]
            public required string Test { get; set; }
        }

        [Fact]
        public void Json()
        {
            var converter = GetConverter<JsonValueConverter>();

            var json = new JsonTest
            {
                Test = "hello world"
            };

            AssertConversion(
                _global,
                converter,
                value =>
                {
                    Assert.Equal(@"{""test"":""hello world""}", value.StringValue);
                },
                FieldType.Json,
                @"""{\u0022test\u0022:\u0022hello world\u0022}""",
                json);
        }

        [Fact]
        public void Key_Untyped()
        {
            var converter = GetConverter(FieldType.Key);

            var key = new UntypedKey(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 1)
                }
            });

            AssertConversion(
                _global,
                converter,
                value =>
                {
                    Assert.Equal(key, new UntypedKey(value.KeyValue));
                },
                FieldType.Key,
                "\"#v1|project||key:id=1\"",
                key);
        }

        [Fact]
        public void Key_Typed()
        {
            var converter = GetConverter(FieldType.Key);

            _ = ReferenceModelCache.Get<ConverterTestModel>();

            var key = new Key<ConverterTestModel>(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 1)
                }
            });

            AssertConversion(
                _global,
                converter,
                value =>
                {
                    Assert.Equal(key, new Key<ConverterTestModel>(value.KeyValue));
                },
                FieldType.Key,
                "\"#v1|project||key:id=1\"",
                key);
        }

        [Fact]
        public void GlobalKey_Untyped()
        {
            var converter = GetConverter(FieldType.GlobalKey);

            var key = new UntypedKey(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 1)
                }
            });

            AssertConversion(
                _local,
                converter,
                value =>
                {
                    Assert.Equal(key, new UntypedKey(value.KeyValue));
                },
                FieldType.GlobalKey,
                "\"#v1|project||key:id=1\"",
                key);
        }

        [Fact]
        public void GlobalKey_Typed()
        {
            var converter = GetConverter(FieldType.GlobalKey);

            _ = ReferenceModelCache.Get<ConverterTestModel>();

            var key = new Key<ConverterTestModel>(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 1)
                }
            });

            AssertConversion(
                _local,
                converter,
                value =>
                {
                    Assert.Equal(key, new Key<ConverterTestModel>(value.KeyValue));
                },
                FieldType.GlobalKey,
                "\"#v1|project||key:id=1\"",
                key);
        }

        [Fact]
        public void LocalKey_Untyped()
        {
            var converter = GetConverter(FieldType.LocalKey);

            var key = new UntypedKey(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                    NamespaceId = "local",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 1)
                }
            });

            AssertConversion(
                _global,
                converter,
                value =>
                {
                    Assert.Equal(key, new UntypedKey(value.KeyValue));
                },
                FieldType.LocalKey,
                "\"#v1|project|local|key:id=1\"",
                key);
        }

        [Fact]
        public void LocalKey_Typed()
        {
            var converter = GetConverter(FieldType.LocalKey);

            _ = ReferenceModelCache.Get<ConverterTestModel>();

            var key = new Key<ConverterTestModel>(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                    NamespaceId = "local",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 1)
                }
            });

            AssertConversion(
                _global,
                converter,
                value =>
                {
                    Assert.Equal(key, new Key<ConverterTestModel>(value.KeyValue));
                },
                FieldType.LocalKey,
                "\"#v1|project|local|key:id=1\"",
                key);
        }

        [Fact]
        public void KeyArray_Untyped()
        {
            var converter = GetConverter(FieldType.KeyArray);

            var key1 = new UntypedKey(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 1)
                }
            });
            var key2 = new UntypedKey(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 2)
                }
            });
            var key3 = new UntypedKey(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 3)
                }
            });
            var keys = new UntypedKey[]
            {
                key1,
                key2,
                key3,
            };

            AssertConversion(
                _global,
                converter,
                value =>
                {
                    Assert.Equal(3, value.ArrayValue.Values.Count);
                    Assert.Equal(key1, new UntypedKey(value.ArrayValue.Values[0].KeyValue));
                    Assert.Equal(key2, new UntypedKey(value.ArrayValue.Values[1].KeyValue));
                    Assert.Equal(key3, new UntypedKey(value.ArrayValue.Values[2].KeyValue));
                },
                FieldType.KeyArray,
                "[\"#v1|project||key:id=1\",\"#v1|project||key:id=2\",\"#v1|project||key:id=3\"]",
                keys);
        }

        [Fact]
        public void KeyArray_Typed()
        {
            var converter = GetConverter(FieldType.KeyArray);

            _ = ReferenceModelCache.Get<ConverterTestModel>();

            var key1 = new Key<ConverterTestModel>(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 1)
                }
            });
            var key2 = new Key<ConverterTestModel>(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 2)
                }
            });
            var key3 = new Key<ConverterTestModel>(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 3)
                }
            });
            var keys = new Key<ConverterTestModel>[]
            {
                key1,
                key2,
                key3,
            };

            AssertConversion(
                _global,
                converter,
                value =>
                {
                    Assert.Equal(3, value.ArrayValue.Values.Count);
                    Assert.Equal(key1, new Key<ConverterTestModel>(value.ArrayValue.Values[0].KeyValue));
                    Assert.Equal(key2, new Key<ConverterTestModel>(value.ArrayValue.Values[1].KeyValue));
                    Assert.Equal(key3, new Key<ConverterTestModel>(value.ArrayValue.Values[2].KeyValue));
                },
                FieldType.KeyArray,
                "[\"#v1|project||key:id=1\",\"#v1|project||key:id=2\",\"#v1|project||key:id=3\"]",
                keys);
        }

        [Fact]
        public void GlobalKeyArray_Untyped()
        {
            var converter = GetConverter(FieldType.GlobalKeyArray);

            var key1 = new UntypedKey(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 1)
                }
            });
            var key2 = new UntypedKey(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 2)
                }
            });
            var key3 = new UntypedKey(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 3)
                }
            });

            AssertConversion(
                _local,
                converter,
                value =>
                {
                    Assert.Equal(3, value.ArrayValue.Values.Count);
                    Assert.Equal(key1, new UntypedKey(value.ArrayValue.Values[0].KeyValue));
                    Assert.Equal(key2, new UntypedKey(value.ArrayValue.Values[1].KeyValue));
                    Assert.Equal(key3, new UntypedKey(value.ArrayValue.Values[2].KeyValue));
                },
                FieldType.GlobalKeyArray,
                "[\"#v1|project||key:id=1\",\"#v1|project||key:id=2\",\"#v1|project||key:id=3\"]",
                new UntypedKey[]
                {
                    key1,
                    key2,
                    key3,
                });
            AssertConversion(
                _local,
                converter,
                value =>
                {
                    Assert.Equal(3, value.ArrayValue.Values.Count);
                    Assert.Equal(key1, new UntypedKey(value.ArrayValue.Values[0].KeyValue));
                    Assert.Equal(key2, new UntypedKey(value.ArrayValue.Values[1].KeyValue));
                    Assert.Equal(key3, new UntypedKey(value.ArrayValue.Values[2].KeyValue));
                },
                FieldType.GlobalKeyArray,
                "[\"#v1|project||key:id=1\",\"#v1|project||key:id=2\",\"#v1|project||key:id=3\"]",
                new List<UntypedKey>
                {
                    key1,
                    key2,
                    key3,
                });
        }

        [Fact]
        public void GlobalKeyArray_Typed()
        {
            var converter = GetConverter(FieldType.GlobalKeyArray);

            _ = ReferenceModelCache.Get<ConverterTestModel>();

            var key1 = new Key<ConverterTestModel>(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 1)
                }
            });
            var key2 = new Key<ConverterTestModel>(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 2)
                }
            });
            var key3 = new Key<ConverterTestModel>(new Key
            {
                PartitionId = new PartitionId
                {
                    ProjectId = "project",
                },
                Path =
                {
                    new Key.Types.PathElement("key", 3)
                }
            });

            AssertConversion(
                _local,
                converter,
                value =>
                {
                    Assert.Equal(3, value.ArrayValue.Values.Count);
                    Assert.Equal(key1, new Key<ConverterTestModel>(value.ArrayValue.Values[0].KeyValue));
                    Assert.Equal(key2, new Key<ConverterTestModel>(value.ArrayValue.Values[1].KeyValue));
                    Assert.Equal(key3, new Key<ConverterTestModel>(value.ArrayValue.Values[2].KeyValue));
                },
                FieldType.GlobalKeyArray,
                "[\"#v1|project||key:id=1\",\"#v1|project||key:id=2\",\"#v1|project||key:id=3\"]",
                new Key<ConverterTestModel>[]
                {
                    key1,
                    key2,
                    key3,
                });
            AssertConversion(
                _local,
                converter,
                value =>
                {
                    Assert.Equal(3, value.ArrayValue.Values.Count);
                    Assert.Equal(key1, new Key<ConverterTestModel>(value.ArrayValue.Values[0].KeyValue));
                    Assert.Equal(key2, new Key<ConverterTestModel>(value.ArrayValue.Values[1].KeyValue));
                    Assert.Equal(key3, new Key<ConverterTestModel>(value.ArrayValue.Values[2].KeyValue));
                },
                FieldType.GlobalKeyArray,
                "[\"#v1|project||key:id=1\",\"#v1|project||key:id=2\",\"#v1|project||key:id=3\"]",
                new List<Key<ConverterTestModel>>
                {
                    key1,
                    key2,
                    key3,
                });
        }
    }
}
