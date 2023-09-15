namespace Redpoint.RuntimeJson.SourceGenerator
{
    using Microsoft.CodeAnalysis;
    using System.Text;

    [Generator(LanguageNames.CSharp)]
    public class RuntimeJsonSourceGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxContextReceiver is RuntimeJsonSyntaxReceiver receiver))
            {
                return;
            }

            if (receiver.Entries.Count == 0)
            {
                return;
            }

            foreach (var entry in receiver.Entries)
            {
                var sourceBuilder = new StringBuilder();
                sourceBuilder.Append($@"using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Redpoint.RuntimeJson;

#nullable enable
");

                sourceBuilder.Append($@"
namespace {entry.Namespace}
{{
    partial class {entry.Class}
    {{
        public {entry.Class}({entry.JsonSerializerContextType} context)
        {{");
                foreach (var type in entry.SerializableClassNames)
                {
                    var typeSafe = type.Replace('.', '_');
                    sourceBuilder.AppendLine($@"            _instance{typeSafe} = new RuntimeJson_{typeSafe}(context);");
                }
                sourceBuilder.Append($@"
        }}");
                foreach (var type in entry.SerializableClassNames)
                {
                    var typeSafe = type.Replace('.', '_');
                    var typeLast = type.Split('.').Last();
                    sourceBuilder.Append($@"
        private RuntimeJson_{typeSafe} _instance{typeSafe};

        public IRuntimeJson<{type}> {typeLast} => _instance{typeSafe};

        private class RuntimeJson_{typeSafe} : IRuntimeJson<{type}>
        {{
            private {entry.JsonSerializerContextType} _serializer;

            public RuntimeJson_{typeSafe}({entry.JsonSerializerContextType} serializer)
            {{
                _serializer = serializer;
            }}

            public Type Type => typeof({type});
            
            public JsonTypeInfo JsonTypeInfo => _serializer.{typeLast};

            public JsonSerializerContext JsonSerializerContext => _serializer;

            public object Deserialize(ref Utf8JsonReader reader)
            {{
                return JsonSerializer.Deserialize(ref reader, _serializer.{typeLast})!;
            }}

            public void Serialize(Utf8JsonWriter writer, object value)
            {{
                JsonSerializer.Serialize(writer, ({type})value, _serializer.{typeLast});
            }}
        }}");
                }
                sourceBuilder.Append($@"
    }}
}}
");

                context.AddSource($"{entry.Class}.RuntimeJsonProviders.g.cs", sourceBuilder.ToString());
            }
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            var syntaxReceiver = new RuntimeJsonSyntaxReceiver();
            context.RegisterForSyntaxNotifications(() => syntaxReceiver);
        }
    }
}