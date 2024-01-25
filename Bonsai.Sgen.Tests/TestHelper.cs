using NJsonSchema;

namespace Bonsai.Sgen.Tests
{
    static class TestHelper
    {
        public static CSharpCodeDomGenerator CreateGenerator(
            JsonSchema schema,
            SerializerLibraries serializerLibraries = SerializerLibraries.YamlDotNet | SerializerLibraries.NewtonsoftJson)
        {
            var settings = new CSharpCodeDomGeneratorSettings
            {
                Namespace = nameof(TestHelper),
                SerializerLibraries = serializerLibraries
            };
            schema = schema.WithCompatibleDefinitions(settings.TypeNameGenerator)
                           .WithResolvedDiscriminatorInheritance();

            return new CSharpCodeDomGenerator(schema, settings);
        }
    }
}
