using Microsoft.VisualStudio.TestTools.UnitTesting;
using NJsonSchema;
using System.Threading.Tasks;

namespace Bonsai.Sgen.Tests
{
    [TestClass]
    public class ToStringGenerationTests
    {
        private static Task<JsonSchema> CreateTestSchema()
        {
            return JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""properties"": {
      ""BaseType"": {
        ""oneOf"": [
          {
            ""$ref"": ""#/definitions/BaseType""
          },
          {
            ""type"": ""null""
          }
        ]
      }
    },
    ""definitions"": {
      ""DerivedType"": {
        ""type"": ""object"",
        ""properties"": {
          ""Bar"": {
            ""type"": [
              ""null"",
              ""string""
            ]
          },
          ""Baz"": {
            ""type"": [
              ""null"",
              ""integer""
            ]
          }
        },
        ""allOf"": [
          {
            ""$ref"": ""#/definitions/BaseType""
          }
        ]
      },
      ""EmptyDerivedType"": {
        ""type"": ""object"",
        ""allOf"": [
          {
            ""$ref"": ""#/definitions/BaseType""
          }
        ]
      },
      ""BaseType"": {
        ""type"": ""object""
      }
    }
  }
");
        }

        [TestMethod]
        public async Task GenerateToStringOverride_FormatMultipleProperties()
        {
            var schema = await CreateTestSchema();
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            CompilerTestHelper.CompileFromSource(code);
        }
    }
}
