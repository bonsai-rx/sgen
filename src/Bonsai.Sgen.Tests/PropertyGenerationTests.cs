using Microsoft.VisualStudio.TestTools.UnitTesting;
using NJsonSchema;

namespace Bonsai.Sgen.Tests
{
    [TestClass]
    public class PropertyGenerationTests
    {
        public class FooTimeSpan
        {
            public TimeSpan Bar { get; set; }

            public TimeSpan? Baz { get; set; }
        }

        public class FooDateTime
        {
            public DateTimeOffset Bar { get; set; }

            public DateTimeOffset? Baz { get; set; }
        }

        [TestMethod]
        public void GenerateTimeSpanProperties_EnsureKnownTypesAndXmlSerialization()
        {
            var schema = JsonSchema.FromType<FooTimeSpan>();
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("public string BarXml"));
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public void GenerateDateTimeProperties_EnsureKnownTypesAndXmlSerialization()
        {
            var schema = JsonSchema.FromType<FooDateTime>();
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("public string BarXml"));
            CompilerTestHelper.CompileFromSource(code);
        }

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
        public async Task GenerateFromSimplePropertyDefault_EnsureDefaultInitializer()
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
            Assert.IsTrue(code.Contains("_name = \"default_name\""), "Missing field initializer.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateFromArrayProperty_EnsureDefaultInitializer()
        {
            var schema = await JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""properties"": {
      ""items"": {
        ""type"": ""array"",
        ""items"": { ""type"": ""string"" }
      }
    }
}
");
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("_items = new System.Collections.Generic.List<string>();"), "Missing field initializer.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateFromComplexPropertyDefault_EnsureFieldInitializer()
        {
            var schema = await JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""definitions"": {
      ""Bar"": {
        ""properties"": {
          ""value"": {
            ""default"": 1,
            ""type"": ""integer""
          },
          ""label"": {
            ""default"": ""default"",
            ""type"": ""string""
          }
        },
        ""title"": ""Bar"",
        ""type"": ""object""
      },
      ""Foo"": {
        ""properties"": {
          ""foo_label"": {
            ""default"": ""foo_default_label"",
            ""type"": ""string""
          },
          ""bar_with_default"": {
            ""allOf"": [{
              ""$ref"": ""#/definitions/Bar""
            }],
            ""default"": {
              ""value"": 0,
              ""label"": ""foo_default""
            }
          }
        },
        ""title"": ""Foo"",
        ""type"": ""object""
      }
    },
    ""properties"": {
      ""foo_with_default"": {
        ""allOf"": [{
          ""$ref"": ""#/definitions/Foo""
        }],
        ""default"": {
          ""foo_label"": ""foo_default_label"",
          ""bar_with_default"": {
            ""label"": ""top_default"",
            ""value"": 2
          }
        }
      }
    }
}
");
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("_barWithDefault.Label = \"foo_default\""));
            Assert.IsTrue(code.Contains("_fooWithDefault.BarWithDefault.Value = 2"));
            Assert.IsTrue(code.Contains("_fooWithDefault.BarWithDefault.Label = \"top_default\""));
            CompilerTestHelper.CompileFromSource(code);
        }
    }
}
