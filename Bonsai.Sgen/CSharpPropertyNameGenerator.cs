using NJsonSchema;

namespace Bonsai.Sgen
{
    internal class CSharpPropertyNameGenerator : NJsonSchema.CodeGeneration.CSharp.CSharpPropertyNameGenerator
    {
        public override string Generate(JsonSchemaProperty property)
        {
            var defaultName = base.Generate(property);
            return CSharpNamingConvention.Instance.Apply(defaultName);
        }
    }
}
