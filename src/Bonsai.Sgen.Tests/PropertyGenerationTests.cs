using Microsoft.VisualStudio.TestTools.UnitTesting;
using NJsonSchema;
using System.Xml.Serialization;

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
            Assert.IsTrue(code.Contains("JsonIgnoreAttribute"), "JsonIgnoreAttribute is missing.");
            Assert.IsTrue(code.Contains("YamlIgnoreAttribute"), "YamlIgnoreAttribute is missing.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public void GenerateDateTimeProperties_EnsureKnownTypesAndXmlSerialization()
        {
            var schema = JsonSchema.FromType<FooDateTime>();
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("public string BarXml"));
            Assert.IsTrue(code.Contains("JsonIgnoreAttribute"), "JsonIgnoreAttribute is missing.");
            Assert.IsTrue(code.Contains("YamlIgnoreAttribute"), "YamlIgnoreAttribute is missing.");
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
        [DataRow("uint8", "byte")]
        [DataRow("int8", "sbyte")]
        [DataRow("uint16", "ushort")]
        [DataRow("int16", "short")]
        [DataRow("uint32", "uint")]
        [DataRow("int32", "int")]
        [DataRow("uint64", "ulong")]
        [DataRow("int64", "long")]
        public async Task GenerateFromPropertyIntegerFormat_EnsureMatchingPrimitiveType(string format, string type)
        {
            var schema = await JsonSchema.FromJsonAsync(@$"
{{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""properties"": {{
      ""value"": {{
        ""type"": ""integer"",
        ""format"": ""{format}"",
      }}
    }},
    ""required"": [""value""]
}}
");
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains($"public {type} Value"), "Missing or invalid property definition.");
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
        public async Task GenerateFromUniqueItemsArrayProperty_EnsureSetType()
        {
            var schema = await JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""properties"": {
      ""tags"": {
        ""type"": ""array"",
        ""items"": { ""type"": ""string"" },
        ""uniqueItems"": true
      }
    }
}
");
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("System.Collections.Generic.HashSet<string>"), "Expected HashSet<string> for uniqueItems array.");
            Assert.IsTrue(code.Contains("_tags = new System.Collections.Generic.HashSet<string>();"), "Missing HashSet initializer.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateFromArrayPropertyWithoutUniqueItems_EnsureArrayType()
        {
            var schema = await JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""properties"": {
      ""items"": {
        ""type"": ""array"",
        ""items"": { ""type"": ""string"" },
        ""uniqueItems"": false
      }
    }
}
");
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("System.Collections.Generic.List<string>"), "Expected List<string> when uniqueItems is false.");
            Assert.IsFalse(code.Contains("HashSet"), "Should not generate HashSet when uniqueItems is false.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateFromUniqueItemsArrayOfObjects_EnsureSetType()
        {
            var schema = await JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""definitions"": {
      ""Tag"": {
        ""type"": ""object"",
        ""title"": ""Tag"",
        ""properties"": {
          ""name"": { ""type"": ""string"" }
        }
      }
    },
    ""properties"": {
      ""tags"": {
        ""type"": ""array"",
        ""items"": { ""$ref"": ""#/definitions/Tag"" },
        ""uniqueItems"": true
      }
    }
}
");
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("System.Collections.Generic.HashSet<Tag>"), "Expected HashSet<Tag> for uniqueItems array of objects.");
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

        [TestMethod]
        public async Task GenerateFromAdditionalPropertiesWithPropertyNames_EnsureKeyIsEnumType()
        {
            var schema = await JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""https://json-schema.org/draft-07/schema#"",
    ""$defs"": {
      ""Element"": {
        ""properties"": {
          ""value"": {
            ""type"": ""integer""
          }
        },
        ""type"": ""object""
      },
      ""KeyEnum"": {
        ""enum"": [
          ""Key1"",
          ""Key2""
        ],
        ""type"": ""string""
      }
    },
    ""properties"": {
      ""elements"": {
        ""additionalProperties"": {
          ""$ref"": ""#/$defs/Element""
        },
        ""propertyNames"": {
          ""$ref"": ""#/$defs/KeyEnum""
        },
        ""type"": ""object""
      }
    },
    ""required"": [
      ""elements""
    ],
    ""title"": ""Container"",
    ""type"": ""object""
}
");
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("System.Collections.Generic.Dictionary<KeyEnum, Element>"));
            Assert.IsTrue(code.Contains("new System.Collections.Generic.Dictionary<KeyEnum, Element>"));
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateOptionalProperties_EnsureOneOfResolvesToNullable()
        {
            var schema = await JsonSchema.FromJsonAsync(@"
{
    ""$defs"": {
      ""Element"": {
        ""properties"": {
          ""value"": {
            ""oneOf"": [
              {
                ""type"": ""integer""
              },
              {
                ""type"": ""null""
              }
            ],
            ""default"": 5
          }
        },
        ""type"": ""object""
      }
    },
    ""properties"": {
      ""optional"": {
        ""oneOf"": [
          {
            ""$ref"": ""#/$defs/Element""
          },
          {
            ""type"": ""null""
          }
        ],
        ""default"": null,
      }
    },
    ""title"": ""Container"",
    ""type"": ""object""
}");
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("private int? _value"));
            Assert.IsTrue(code.Contains("_value = 5"), "Default value is not being assigned correctly.");
            Assert.IsFalse(
                code.IndexOf(nameof(XmlIgnoreAttribute)) < code.IndexOf("Value"),
                $"Nullable primitive properties must omit {nameof(XmlIgnoreAttribute)}.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateOptionalProperties_EnsureAnyOfResolvesToNullable()
        {
            var schema = await JsonSchema.FromJsonAsync(@"
{
    ""$defs"": {
      ""Element"": {
        ""properties"": {
          ""value"": {
            ""anyOf"": [
              {
                ""type"": ""integer""
              },
              {
                ""type"": ""null""
              }
            ],
            ""default"": 5
          }
        },
        ""type"": ""object""
      }
    },
    ""properties"": {
      ""optional"": {
        ""anyOf"": [
          {
            ""$ref"": ""#/$defs/Element""
          },
          {
            ""type"": ""null""
          }
        ],
        ""default"": null,
      }
    },
    ""title"": ""Container"",
    ""type"": ""object""
}");
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("private int? _value"));
            Assert.IsTrue(code.Contains("_value = 5"), "Default value is not being assigned correctly.");
            Assert.IsFalse(
                code.IndexOf(nameof(XmlIgnoreAttribute)) < code.IndexOf("Value"),
                $"Nullable primitive properties must omit {nameof(XmlIgnoreAttribute)}.");
            CompilerTestHelper.CompileFromSource(code);
        }
    }
}
