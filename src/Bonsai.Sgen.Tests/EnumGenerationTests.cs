using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NJsonSchema;
using NJsonSchema.Generation;
using System.Xml.Serialization;

namespace Bonsai.Sgen.Tests
{
    [TestClass]
    public class EnumGenerationTests
    {
        public class Foo
        {
            public Bar Bar { get; set; }

            public Bar? Baz { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public Bar Bar2 { get; set; }
        }

        public enum Bar
        {
            A = 0,
            B = 5,
            C = 6
        }

        [TestMethod]
        public void GenerateIntegerEnum_SerializerAnnotationsUseIntegerValues()
        {
            var schema = JsonSchema.FromType<Foo>(new JsonSchemaGeneratorSettings { GenerateEnumMappingDescription = true });
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("/// 0 = A"));
            Assert.IsTrue(code.Contains("EnumMemberAttribute(Value=\"0\")"));
            Assert.IsTrue(code.Contains("YamlMemberAttribute(Alias=\"0\")"));
        }

        [TestMethod]
        public async Task GenerateIntegerEnumWithRawLiterals_EnumTypeUseValidIdentifiers()
        {
            var schema = await JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""additionalProperties"": false,
    ""properties"": {
      ""Enum"": {
         ""$ref"": ""#/definitions/IntEnum""
      }
    },
    ""definitions"": {
      ""IntEnum"": {
         ""enum"": [ 0, 1, 2 ],
         ""type"": ""integer""
      }
    }
  }
");
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public void GenerateStringEnum_SerializerAnnotationsUseStringValues()
        {
            var schema = JsonSchema.FromType<Foo>();
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("EnumMemberAttribute(Value=\"A\")"));
            Assert.IsTrue(code.Contains("YamlMemberAttribute(Alias=\"A\")"));
        }

        [TestMethod]
        public void GenerateStringEnum_OmitXmlIgnoreAttributeInEnumProperties()
        {
            var schema = JsonSchema.FromType<Foo>();
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsFalse(code.Contains(nameof(XmlIgnoreAttribute)), $"Enum properties must omit {nameof(XmlIgnoreAttribute)}.");
        }

        [TestMethod]
        public async Task GenerateStringEnum_StringEnumTypeDefinitionWithStringEnumConverter()
        {
            var schema = await JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""additionalProperties"": false,
    ""properties"": {
      ""Enum"": {
         ""$ref"": ""#/definitions/StringEnum""
      }
    },
    ""definitions"": {
      ""StringEnum"": {
         ""enum"": [
            ""This is a string A"",
            ""This is a string B""
         ],
         ""title"": ""StringEnum"",
         ""type"": ""string""
      }
    }
  }
");
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("StringEnumConverter"));
            CompilerTestHelper.CompileFromSource(code);
        }
    }
}
