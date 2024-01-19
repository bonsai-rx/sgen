using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NJsonSchema;

namespace Bonsai.Sgen.Tests
{
    [TestClass]
    public class CasingGenerationTests
    {
        private static Task<JsonSchema> CreateTestSchema()
        {
            return JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""properties"": {
      ""base_type"": {
        ""oneOf"": [
          {
            ""$ref"": ""#/definitions/thing_container""
          },
          {
            ""type"": ""null""
          }
        ]
      }
    },
    ""definitions"": {
      ""thing_container"": {
        ""type"": ""object"",
        ""properties"": {
          ""bar"": {
            ""enum"": [
               ""This is a string A"",
               ""This is a string B"",
               ""snake_case_with_3_numbers""
            ],
            ""title"": ""StringEnum"",
            ""type"": ""string""
          }
        }
      }
    }
}
");
        }

        [TestMethod]
        public async Task GenerateFromSnakeCase_GeneratePascalCaseNames()
        {
            var schema = await CreateTestSchema();
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("public ThingContainer BaseType"), "Incorrect casing for property type or name.");
            Assert.IsTrue(code.Contains("ThisIsAStringA = 0"), "Incorrect casing for enum name.");
            Assert.IsTrue(code.Contains("SnakeCaseWith3Numbers = 2"), "Incorrect casing for enum name.");
            CompilerTestHelper.CompileFromSource(code);
        }
    }
}
