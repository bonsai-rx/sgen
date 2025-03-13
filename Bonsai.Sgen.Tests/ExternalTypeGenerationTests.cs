using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NJsonSchema;

namespace Bonsai.Sgen.Tests
{
    [TestClass]
    public class ExternalTypeGenerationTests
    {
        private static async Task<JsonSchema> CreateCommonDefinitions()
        {
            return await JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""definitions"": {
      ""CommonType"": {
        ""type"": ""object"",
        ""properties"": {
          ""Bar"": {
            ""type"": ""integer""
          }
        }
      }
    }
  }
");
        }

        [TestMethod]
        public void GenerateWithEmptyDefinitions_EmitEmptyCodeBlock()
        {
            var schema = new JsonSchema();
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateWithDefinitionsOnly_EmitDefinitionTypes()
        {
            var schema = await CreateCommonDefinitions();
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("public partial class CommonType"), "Missing type definition.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateWithExternalTypeReferenceProperty_OmitExternalTypeDefinition()
        {
            var schemaA = await CreateCommonDefinitions();
            var generatorA = TestHelper.CreateGenerator(schemaA, schemaNamespace: $"{nameof(TestHelper)}.Base");
            var codeA = generatorA.GenerateFile();

            var schemaB = await JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""definitions"": {
      ""SpecificType"": {
        ""type"": ""object"",
        ""properties"": {
          ""Bar"": {
            ""$ref"": ""https://schemaA/definitions/CommonType""
          },
          ""Baz"": {
            ""type"": ""integer""
          }
        }
      }
    }
  }
",
documentPath: "",
schema => new TestJsonReferenceResolver(
    new JsonSchemaAppender(schema, new DefaultTypeNameGenerator()),
    schemaA,
    generatorA.Settings.Namespace));

            var generatorB = TestHelper.CreateGenerator(schemaB, schemaNamespace: $"{nameof(TestHelper)}.Derived");
            var codeB = generatorB.GenerateFile();
            CompilerTestHelper.CompileFromSource(codeA, codeB);
        }
    }
}
