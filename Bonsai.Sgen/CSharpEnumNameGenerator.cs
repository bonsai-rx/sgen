using NJsonSchema;
using NJsonSchema.CodeGeneration;

namespace Bonsai.Sgen
{
    internal class CSharpEnumNameGenerator : IEnumNameGenerator
    {
        readonly DefaultEnumNameGenerator defaultGenerator = new();

        public string Generate(int index, string name, object value, JsonSchema schema)
        {
            var defaultName = defaultGenerator.Generate(index, name, value, schema);
            return CSharpNamingConvention.Instance.Apply(defaultName);
        }
    }
}
