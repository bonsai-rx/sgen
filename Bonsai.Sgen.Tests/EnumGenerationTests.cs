using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NJsonSchema;
using NJsonSchema.Generation;

namespace Bonsai.Sgen.Tests
{
    [TestClass]
    public class EnumGenerationTests
    {
        public class Foo
        {
            public Bar Bar { get; set; }

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
        public void GenerateStringEnum_SerializerAnnotationsUseStringValues()
        {
            var schema = JsonSchema.FromType<Foo>();
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("EnumMemberAttribute(Value=\"A\")"));
            Assert.IsTrue(code.Contains("YamlMemberAttribute(Alias=\"A\")"));
        }
    }
}
