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
    }
}
