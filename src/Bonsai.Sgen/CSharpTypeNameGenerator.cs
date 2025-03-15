using NJsonSchema;

namespace Bonsai.Sgen
{
    internal class CSharpTypeNameGenerator : DefaultTypeNameGenerator
    {
        protected override string Generate(JsonSchema schema, string typeNameHint)
        {
            var defaultName = base.Generate(schema, typeNameHint);
            return CSharpNamingConvention.Instance.Apply(defaultName);
        }

        public override string Generate(JsonSchema schema, string typeNameHint, IEnumerable<string> reservedTypeNames)
        {
            if (schema.TryGetExternalTypeName(out string typeName))
                return typeName;

            return base.Generate(schema, typeNameHint, reservedTypeNames);
        }
    }
}
