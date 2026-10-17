using System.Reactive.Linq;
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
            Assert.IsFalse(code.Contains("YamlMemberAttribute(Alias=\"0\")"));
        }

        [TestMethod]
        public async Task GenerateIntegerEnumWithRawLiterals_EnumTypeUseValidIdentifiers()
        {
            var schema = await SchemaTestHelper.FromJsonAsync(@"
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
            Assert.IsFalse(code.Contains("YamlMemberAttribute(Alias=\"A\")"));
        }

        [TestMethod]
        public void GenerateEnumWithYamlSerializer_SerializerAnnotationsUseEnumMemberValues()
        {
            var schema = JsonSchema.FromType<Foo>();
            var generator = TestHelper.CreateGenerator(schema, SerializerLibraries.YamlDotNet);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("EnumMemberAttribute(Value=\"A\")"), "Enum members must declare their serialized value.");
            Assert.IsFalse(code.Contains("YamlMemberAttribute(Alias=\"A\")"), "Enum members must not declare YAML aliases.");
            CompilerTestHelper.CompileFromSource(code);
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
            var schema = await SchemaTestHelper.FromJsonAsync(@"
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

        [TestMethod]
        public async Task GenerateStringEnum_DefaultEnumValueUsesSimpleTypeName()
        {
            var schema = await SchemaTestHelper.FromJsonAsync(@"
{
    ""$defs"": {
      ""EventName"": {
        ""enum"": [
          ""None"",
          ""Event1"",
          ""Event2""
        ],
        ""type"": ""string""
      },
      ""Foo"": {
        ""properties"": {
          ""eventName"": {
            ""$ref"": ""#/$defs/EventName"",
            ""default"": ""None""
          }
        },
        ""type"": ""object""
      }
    },
    ""properties"": {
      ""foo"": {
        ""$ref"": ""#/$defs/Foo""
      }
    },
    ""required"": [
      ""foo""
    ],
    ""title"": ""FooContainer"",
    ""type"": ""object""
}
");

            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: "FooContainer");
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("EventName.None"));
            CompilerTestHelper.CompileFromSource(code);
        }

        static JsonSchema CreateExternalEnumSchema()
        {
            var schema = JsonSchema.FromType<Foo>(new JsonSchemaGeneratorSettings
            {
                SerializerSettings = new JsonSerializerSettings { Converters = { new StringEnumConverter() } }
            });
            var enumSchema = schema.Definitions[nameof(Bar)];
            enumSchema.ExtensionData ??= new Dictionary<string, object>();
            enumSchema.ExtensionData[JsonSchemaExtensions.TypeNameAnnotation] = typeof(Bar).FullName!.Replace('+', '.');
            return schema;
        }

        [TestMethod]
        public void GenerateWithExternalStringEnumProperty_UseStringEnumConverter()
        {
            var schema = CreateExternalEnumSchema();
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsFalse(code.Contains("enum Bar"), "External enumeration must not be generated.");
            Assert.IsTrue(code.Contains("StringEnumConverter"), "Property typed as an external string enumeration must declare the converter.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        [DataRow(SerializerLibraries.NewtonsoftJson, "Json", @"{""Bar"":""B"",""Baz"":""C""}")]
        [DataRow(SerializerLibraries.YamlDotNet, "Yaml", "Bar: B\nBaz: C\n")]
        public async Task GenerateWithExternalStringEnumProperty_RoundTripNames(SerializerLibraries serializerLibraries, string format, string text)
        {
            var schema = CreateExternalEnumSchema();
            var generator = TestHelper.CreateGenerator(schema, serializerLibraries);
            var assembly = CompilerTestHelper.CompileToAssembly(generator.GenerateFile());
            var containerType = SerializerTestHelper.GetGeneratedType(assembly, nameof(Foo));
            var container = await SerializerTestHelper.Deserialize(assembly, $"DeserializeFrom{format}", containerType, text);
            Assert.AreEqual(Bar.B, await WorkflowTestHelper.Select(container, nameof(Foo.Bar)));
            Assert.AreEqual(Bar.C, await WorkflowTestHelper.Select(container, nameof(Foo.Baz)), "Nullable external enumeration must read its name.");

            var output = await SerializerTestHelper.Serialize(assembly, $"SerializeTo{format}", containerType, container);
            Assert.IsFalse(output.Contains('5') || output.Contains('6'), "Serialized external enumeration must carry its name instead of its value.");
        }
    }
}
