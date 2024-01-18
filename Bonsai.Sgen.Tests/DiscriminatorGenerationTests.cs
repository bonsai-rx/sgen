using Microsoft.VisualStudio.TestTools.UnitTesting;
using NJsonSchema;
using System.Threading.Tasks;

namespace Bonsai.Sgen.Tests
{
    [TestClass]
    public class DiscriminatorGenerationTests
    {
        static void AssertDiscriminatorAttribute(string code, SerializerLibraries serializerLibraries, string discriminatorName)
        {
            if (serializerLibraries.HasFlag(SerializerLibraries.NewtonsoftJson))
            {
                Assert.IsTrue(
                    code.Contains($"[Newtonsoft.Json.JsonConverter(typeof(JsonInheritanceConverter), \"{discriminatorName}\")]"),
                    message: "Missing JSON discriminator attribute.");
            }
            if (serializerLibraries.HasFlag(SerializerLibraries.YamlDotNet))
            {
                Assert.IsTrue(
                    code.Contains($"[YamlDiscriminator(\"{discriminatorName}\")]"),
                    message: "Missing YAML discriminator attribute.");
            }
        }

        [TestMethod]
        [DataRow(SerializerLibraries.YamlDotNet)]
        [DataRow(SerializerLibraries.NewtonsoftJson)]
        [DataRow(SerializerLibraries.NewtonsoftJson | SerializerLibraries.YamlDotNet)]
        public async Task GenerateFromAnyOfDiscriminatorSchema_SerializerAnnotationsDeclareKnownTypes(SerializerLibraries serializerLibraries)
        {
            var schema = await JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""additionalProperties"": false,
    ""properties"": {
      ""Animal"": {
        ""oneOf"": [
          {
            ""$ref"": ""#/definitions/Animal""
          },
          {
            ""type"": ""null""
          }
        ]
      }
    },
    ""definitions"": {
      ""Dog"": {
        ""type"": ""object"",
        ""additionalProperties"": false,
        ""properties"": {
          ""Bar"": {
            ""type"": [
              ""null"",
              ""string""
            ]
          }
        },
        ""allOf"": [
          {
            ""$ref"": ""#/definitions/Animal""
          }
        ]
      },
      ""Animal"": {
        ""type"": ""object"",
        ""discriminator"": {
          ""propertyName"": ""discriminator"",
          ""mapping"": {
              ""DogType"": ""#/definitions/Dog""
          }
        },
        ""x-abstract"": true,
        ""additionalProperties"": false,
        ""required"": [
          ""discriminator""
        ],
        ""properties"": {
          ""Foo"": {
            ""type"": [
              ""null"",
              ""string""
            ]
          },
          ""discriminator"": {
            ""type"": ""string""
          }
        }
      }
    }
  }
");
            var generator = TestHelper.CreateGenerator(schema, serializerLibraries);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("[JsonInheritanceAttribute(\"DogType\", typeof(Dog))]"));
            AssertDiscriminatorAttribute(code, serializerLibraries, "discriminator");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        [DataRow(SerializerLibraries.YamlDotNet)]
        [DataRow(SerializerLibraries.NewtonsoftJson)]
        [DataRow(SerializerLibraries.NewtonsoftJson | SerializerLibraries.YamlDotNet)]
        public async Task GenerateFromOneOfDiscriminatorSchema_SerializerAnnotationsDeclareKnownTypes(SerializerLibraries serializerLibraries)
        {
            var schema = await JsonSchema.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""properties"": {
      ""Animals"": {
        ""type"": ""array"",
        ""items"": { ""$ref"": ""#/definitions/AnimalTypes"" }
      }
    },
    ""definitions"": {
      ""Dog"": {
        ""type"": ""object"",
        ""properties"": {
          ""kind"": {
            ""enum"": [ ""Dog"" ]
          },
          ""Bar"": {
            ""type"": [
              ""null"",
              ""string""
            ]
          }
        },
        ""required"": [ ""kind"", ""Bar"" ],
        ""allOf"": [
          {
            ""$ref"": ""#/definitions/Animal""
          }
        ]
      },
      ""Cat"": {
        ""type"": ""object"",
        ""properties"": {
          ""kind"": {
            ""enum"": [ ""Cat"" ]
          },
          ""Baz"": {
            ""type"": [
              ""null"",
              ""string""
            ]
          }
        },
        ""required"": [ ""kind"", ""Baz"" ],
        ""allOf"": [
          {
            ""$ref"": ""#/definitions/Animal""
          }
        ]
      },
      ""Animal"": {
        ""type"": ""object"",
        ""discriminator"": ""kind"",
        ""x-abstract"": true,
        ""required"": [
          ""kind""
        ],
        ""properties"": {
          ""Foo"": {
            ""type"": [
              ""null"",
              ""string""
            ]
          },
          ""kind"": {
            ""type"": ""string""
          }
        }
      },
      ""AnimalTypes"": {
          ""oneOf"": [
            {
              ""$ref"": ""#/definitions/Dog""
            },
            {
              ""$ref"": ""#/definitions/Cat""
            },
            {
              ""type"": ""null""
            }
          ]
      }
    }
  }
");
            var generator = TestHelper.CreateGenerator(schema, serializerLibraries);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("class Dog : Animal"), "Derived types do not inherit from base type.");
            Assert.IsTrue(!code.Contains("public enum DogKind"), "Discriminator property is repeated in derived types.");
            Assert.IsTrue(code.Contains("List<Animal> Animals"), "Container array element type does not match base type.");
            Assert.IsTrue(code.Contains("[JsonInheritanceAttribute(\"Dog\", typeof(Dog))]"));
            AssertDiscriminatorAttribute(code, serializerLibraries, "kind");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        [DataRow(SerializerLibraries.YamlDotNet)]
        [DataRow(SerializerLibraries.NewtonsoftJson)]
        [DataRow(SerializerLibraries.NewtonsoftJson | SerializerLibraries.YamlDotNet)]
        public async Task GenerateFromOneOfDiscriminatorSchemaProperty_SerializerAnnotationsDeclareKnownTypes(SerializerLibraries serializerLibraries)
        {
            var schema = await JsonSchema.FromJsonAsync(@"
{
  ""$schema"": ""http://json-schema.org/draft-04/schema#"",
  ""type"": ""object"",
  ""title"": ""Container"",
  ""properties"": {
    ""Animal"": {
      ""x-abstract"": true,
      ""discriminator"": {
        ""propertyName"": ""kind"",
        ""mapping"": {
          ""Dog"": ""#/definitions/Dog"",
          ""Cat"": ""#/definitions/Cat""
        }
      },
      ""oneOf"": [
        {
          ""$ref"": ""#/definitions/Dog""
        },
        {
          ""$ref"": ""#/definitions/Cat""
        },
        {
          ""type"": ""null""
        }
      ]
    }
  },
  ""definitions"": {
    ""Dog"": {
      ""type"": ""object"",
      ""properties"": {
        ""kind"": {
          ""enum"": [ ""Dog"" ]
        },
        ""Bar"": {
          ""type"": [
            ""null"",
            ""string""
          ]
        }
      },
      ""required"": [ ""kind"", ""Bar"" ]
    },
    ""Cat"": {
      ""type"": ""object"",
      ""properties"": {
        ""kind"": {
          ""enum"": [ ""Cat"" ]
        },
        ""Baz"": {
          ""type"": [
            ""null"",
            ""string""
          ]
        }
      },
      ""required"": [ ""kind"", ""Baz"" ]
    }
  }
}
");
            var generator = TestHelper.CreateGenerator(schema, serializerLibraries);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("class Dog : Animal"), "Derived types do not inherit from base type.");
            Assert.IsTrue(!code.Contains("public enum DogKind"), "Discriminator property is repeated in derived types.");
            Assert.IsTrue(code.Contains("Animal Animal"), "Container element type does not match base type.");
            Assert.IsTrue(code.Contains("[JsonInheritanceAttribute(\"Dog\", typeof(Dog))]"));
            AssertDiscriminatorAttribute(code, serializerLibraries, "kind");
            CompilerTestHelper.CompileFromSource(code);
        }
    }
}
