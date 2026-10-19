using Microsoft.VisualStudio.TestTools.UnitTesting;
using NJsonSchema;

namespace Bonsai.Sgen.Tests
{
    [TestClass]
    public class CasingGenerationTests
    {
        private static Task<JsonSchema> CreateTestSchema()
        {
            return SchemaTestHelper.FromJsonAsync(@"
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
               ""snake_case_with_3_numbers"",
               ""hyphen-case-with-3-numbers""
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
        public async Task GenerateFromSnakeAndHyphenCase_GeneratePascalCaseNames()
        {
            var schema = await CreateTestSchema();
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("public ThingContainer BaseType"), "Incorrect casing for property type or name.");
            Assert.IsTrue(code.Contains("ThisIsAStringA = 0"), "Incorrect casing for enum name.");
            Assert.IsTrue(code.Contains("SnakeCaseWith3Numbers = 2"), "Incorrect casing for enum name.");
            Assert.IsTrue(code.Contains("HyphenCaseWith3Numbers = 3"), "Incorrect casing for enum name.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateFromCamelCaseAndDigits_KeepCapitals()
        {
            var schema = await SchemaTestHelper.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""properties"": {
      ""paw_2d"": { ""type"": ""string"" },
      ""claw2d"": { ""type"": ""string"" },
      ""digitalIO"": { ""type"": ""string"" },
      ""activityCenter"": { ""type"": ""string"" },
      ""_2d"": { ""type"": ""string"" },
      ""_id"": { ""type"": ""string"" },
      ""__dunder"": { ""type"": ""string"" },
      ""shape"": {
        ""enum"": [""rgb_2d"", ""flat""],
        ""title"": ""Shape"",
        ""type"": ""string""
      }
    }
}
");
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("public string Paw2d"), "Letter after digit must keep its case.");
            Assert.IsTrue(code.Contains("public string Claw2d"), "Letter after digit must keep its case without separator.");
            Assert.IsTrue(code.Contains("public string DigitalIO"), "Capitals of camelCase name must be kept.");
            Assert.IsTrue(code.Contains("public string ActivityCenter"), "Capitals of camelCase name must be kept.");
            Assert.IsTrue(code.Contains("public string _2d"), "Leading digit must be prefixed with underscore.");
            Assert.IsTrue(code.Contains("public string Id"), "Leading underscore must be dropped before letter.");
            Assert.IsTrue(code.Contains("public string Dunder"), "Leading underscores must be dropped before letter.");
            Assert.IsTrue(code.Contains("Rgb2d = 0"), "Incorrect casing for enum name.");
            CompilerTestHelper.CompileFromSource(code);
        }
    }
}
