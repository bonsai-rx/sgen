using Microsoft.VisualStudio.TestTools.UnitTesting;
using NJsonSchema;
using System.Threading.Tasks;

namespace Bonsai.Sgen.Tests
{
    [TestClass]
    public class DiscriminatorGenerationTests
    {
        [TestMethod]
        public async Task GenerateFromAnyOfDiscriminatorSchema_SerializerAnnotationsDeclareKnownTypes()
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
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("[JsonInheritanceAttribute(\"DogType\", typeof(Dog))]"));
            Assert.IsTrue(code.Contains("[YamlDiscriminator(\"discriminator\")]"));
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateFromOneOfDiscriminatorSchema_SerializerAnnotationsDeclareKnownTypes()
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
            ""type"": ""string"",
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
            ""type"": ""string"",
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
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("class Dog : Animal"), "Derived types do not inherit from base type.");
            Assert.IsTrue(!code.Contains("public enum DogKind"), "Discriminator property is repeated in derived types.");
            Assert.IsTrue(code.Contains("List<Animal> Animals"), "Container array element type does not match base type.");
            Assert.IsTrue(code.Contains("[JsonInheritanceAttribute(\"Dog\", typeof(Dog))]"));
            Assert.IsTrue(code.Contains("[YamlDiscriminator(\"kind\")]"));
            CompilerTestHelper.CompileFromSource(code);
        }
    }
}
