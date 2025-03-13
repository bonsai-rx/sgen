using NJsonSchema;

namespace Bonsai.Sgen.Tests
{
    static class TestHelper
    {
        public static CSharpCodeDomGenerator CreateGenerator(
            JsonSchema schema,
            SerializerLibraries serializerLibraries = SerializerLibraries.YamlDotNet | SerializerLibraries.NewtonsoftJson,
            string schemaNamespace = nameof(TestHelper))
        {
            var settings = new CSharpCodeDomGeneratorSettings
            {
                Namespace = schemaNamespace,
                SerializerLibraries = serializerLibraries
            };
            schema = schema.WithCompatibleDefinitions(settings.TypeNameGenerator)
                           .WithResolvedDiscriminatorInheritance();

            return new CSharpCodeDomGenerator(schema, settings);
        }
    }
}
