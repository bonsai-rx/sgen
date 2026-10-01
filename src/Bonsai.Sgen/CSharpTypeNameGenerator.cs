using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;
using NJsonSchema.Visitors;

namespace Bonsai.Sgen
{
    internal class CSharpTypeNameGenerator(CSharpGeneratorSettings settings) : DefaultTypeNameGenerator
    {
        const char NamespaceSeparator = '.';
        readonly CSharpGeneratorSettings _settings = settings;
        readonly HashSet<string> _annotatedTypeNames = new();
        readonly Dictionary<string, AnnotatedDefinition> _internalTypeNames = new();

        public void ReserveAnnotatedTypeNames(JsonSchema rootObject)
        {
            var visitor = new TypeNameAnnotationVisitor(_annotatedTypeNames);
            visitor.Visit(rootObject);
        }

        protected override string Generate(JsonSchema schema, string typeNameHint)
        {
            var defaultName = base.Generate(schema, typeNameHint);
            return CSharpNamingConvention.Instance.Apply(defaultName);
        }

        public override string Generate(JsonSchema schema, string typeNameHint, IEnumerable<string> reservedTypeNames)
        {
            if (schema.TryGetExternalTypeName(out string typeName))
            {
                if (!NamespaceEquals(typeName, _settings.Namespace))
                    return typeName;

                return GenerateInternalTypeName(schema, typeName, typeNameHint);
            }

            var internalTypeNames = from annotatedTypeName in _annotatedTypeNames
                                    where NamespaceEquals(annotatedTypeName, _settings.Namespace)
                                    select GetTypeNameWithoutNamespace(annotatedTypeName);
            return base.Generate(schema, typeNameHint, reservedTypeNames.Concat(internalTypeNames).ToHashSet());
        }

        private string GenerateInternalTypeName(JsonSchema schema, string typeName, string typeNameHint)
        {
            var localTypeName = GetTypeNameWithoutNamespace(typeName);
            if (_internalTypeNames.TryGetValue(localTypeName, out var existing))
            {
                if (existing.Schema != schema.ActualSchema)
                {
                    throw new InvalidOperationException(
                        $"The definitions '{existing.TypeNameHint}' and '{typeNameHint}' are both annotated " +
                        $"with the type name '{typeName}'.");
                }
            }
            else _internalTypeNames.Add(localTypeName, new AnnotatedDefinition(schema.ActualSchema, typeNameHint));
            return localTypeName;
        }

        public string GenerateNamespace(JsonSchema schema, string namespaceNameHint)
        {
            var parts = namespaceNameHint.Split(NamespaceSeparator, StringSplitOptions.RemoveEmptyEntries);
            var partIdentifiers = Array.ConvertAll(parts, part => Generate(schema, part));
            return string.Join(NamespaceSeparator, partIdentifiers);
        }

        internal static ReadOnlySpan<char> GetNamespaceFromTypeName(string typeName)
        {
            ArgumentNullException.ThrowIfNull(typeName, nameof(typeName));
            var typeNameSeparatorIndex = typeName.LastIndexOf(NamespaceSeparator);
            if (typeNameSeparatorIndex <= 0)
                return string.Empty;

            return typeName.AsSpan(..typeNameSeparatorIndex);
        }

        internal static string GetTypeNameWithoutNamespace(string typeName)
        {
            ArgumentNullException.ThrowIfNull(typeName, nameof(typeName));
            var typeNameSeparatorIndex = typeName.LastIndexOf(NamespaceSeparator);
            return typeName[(typeNameSeparatorIndex + 1)..];
        }

        internal static bool NamespaceEquals(string typeName, string ns)
        {
            return MemoryExtensions.Equals(GetNamespaceFromTypeName(typeName), ns, StringComparison.Ordinal);
        }

        record AnnotatedDefinition(JsonSchema Schema, string TypeNameHint);

        class TypeNameAnnotationVisitor(HashSet<string> annotatedTypeNames) : JsonSchemaVisitorBase
        {
            protected override JsonSchema VisitSchema(JsonSchema schema, string path, string typeNameHint)
            {
                if (schema.TryGetExternalTypeName(out string typeName))
                    annotatedTypeNames.Add(typeName);

                return schema;
            }
        }
    }
}
