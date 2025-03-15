using NJsonSchema;
using NJsonSchema.References;
using NJsonSchema.Visitors;

namespace Bonsai.Sgen.Tests
{
    internal class TestJsonReferenceResolver : JsonReferenceResolver
    {
        readonly IJsonReference _jsonReference;
        readonly IDictionary<string, JsonSchema> _definitions;

        public TestJsonReferenceResolver(JsonSchemaAppender schemaAppender, JsonSchema schema, string ns)
            : base(schemaAppender)
        {
            _jsonReference = schema;
            _definitions = schema.Definitions;
            new ReferenceSchemaVisitor(ns).Visit(_jsonReference);
        }

        private IJsonReference ResolveLocalReference(string path)
        {
            var typeName = Path.GetFileName(path);
            return _definitions[typeName];
        }

        public override Task<IJsonReference> ResolveFileReferenceAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ResolveLocalReference(filePath));
        }

        public override Task<IJsonReference> ResolveUrlReferenceAsync(string url, CancellationToken cancellationToken = default)
        {
            var referencePath = new Uri(url).LocalPath;
            return Task.FromResult(ResolveLocalReference(referencePath));
        }

        class ReferenceSchemaVisitor : JsonSchemaVisitorBase
        {
            readonly string _namespace;

            public ReferenceSchemaVisitor(string ns)
            {
                _namespace = ns;
            }

            protected override JsonSchema VisitSchema(JsonSchema schema, string path, string typeNameHint)
            {
                if (!string.IsNullOrEmpty(typeNameHint))
                {
                    schema.ExtensionData ??= new Dictionary<string, object>();
                    schema.ExtensionData[JsonSchemaExtensions.TypeNameAnnotation] = $"{_namespace}.{typeNameHint}";
                }
                return schema;
            }
        }
    }
}
