using NJsonSchema;

namespace Bonsai.Sgen.Tests
{
    static class TestHelper
    {
        public static CSharpCodeDomGenerator CreateGenerator(
            JsonSchema schema,
            SerializerLibraries serializerLibraries = SerializerLibraries.YamlDotNet | SerializerLibraries.NewtonsoftJson)
        {
            schema = schema.WithUniqueDiscriminatorProperties();
            var settings = new CSharpCodeDomGeneratorSettings
            {
                Namespace = nameof(TestHelper),
                SerializerLibraries = serializerLibraries
            };

            return new CSharpCodeDomGenerator(schema, settings);
        }
    }
}
