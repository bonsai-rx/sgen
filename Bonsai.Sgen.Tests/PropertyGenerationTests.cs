using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NJsonSchema;

namespace Bonsai.Sgen.Tests
{
    [TestClass]
    public class PropertyGenerationTests
    {
        [TestMethod]
        public async Task GenerateFromRequiredNullableProperty_EnsurePropertyAnnotation()
        {
            var schema = await JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""properties"": {
      ""name"": {
        ""oneOf"": [
          {
            ""type"": ""string""
          },
          {
            ""type"": ""null""
          }
        ]
      }
    },
    ""required"": [""name""]
}
");
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("Required=Newtonsoft.Json.Required.AllowNull"), "Missing property annotation.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateFromPropertyDefault_EnsureFieldInitializer()
        {
            var schema = await JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""properties"": {
      ""name"": {
        ""default"": ""default_name"",
        ""type"": ""string""
      }
    }
}
");
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("private string _name = \"default_name\""), "Missing field initializer.");
            CompilerTestHelper.CompileFromSource(code);
        }
    }
}
