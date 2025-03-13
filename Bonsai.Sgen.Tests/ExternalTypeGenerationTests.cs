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
            CompilerTestHelper.CompileFromSource(code);
        }
    }
}
