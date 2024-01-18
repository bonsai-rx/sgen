using NJsonSchema;
using YamlDotNet.Serialization.NamingConventions;

namespace Bonsai.Sgen
{
    internal class CSharpTypeNameGenerator : DefaultTypeNameGenerator
    {
        protected override string Generate(JsonSchema schema, string typeNameHint)
        {
            var defaultName = base.Generate(schema, typeNameHint);
            return PascalCaseNamingConvention.Instance.Apply(defaultName);
        }
    }
}
