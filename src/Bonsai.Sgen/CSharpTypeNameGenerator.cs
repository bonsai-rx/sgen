using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;

namespace Bonsai.Sgen
{
    internal class CSharpTypeNameGenerator(CSharpGeneratorSettings settings) : DefaultTypeNameGenerator
    {
        const char NamespaceSeparator = '.';
        readonly CSharpGeneratorSettings _settings = settings;

        protected override string Generate(JsonSchema schema, string typeNameHint)
        {
            var defaultName = base.Generate(schema, typeNameHint);
            return CSharpNamingConvention.Instance.Apply(defaultName);
        }

        public override string Generate(JsonSchema schema, string typeNameHint, IEnumerable<string> reservedTypeNames)
        {
            if (schema.TryGetExternalTypeName(out string typeName) && !NamespaceEquals(typeName, _settings.Namespace))
                return typeName;

            return base.Generate(schema, typeNameHint, reservedTypeNames);
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

        internal static bool NamespaceEquals(string typeName, string ns)
        {
            return MemoryExtensions.Equals(GetNamespaceFromTypeName(typeName), ns, StringComparison.Ordinal);
        }
    }
}
