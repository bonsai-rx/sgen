using Microsoft.VisualStudio.TestTools.UnitTesting;
using NJsonSchema;
using System.Collections.Generic;
using System.Linq;
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
        public async Task GenerateFromAllOfDiscriminatorSchema_SerializerAnnotationsDeclareKnownTypes(SerializerLibraries serializerLibraries)
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
        public void GenerateFromOneOfDiscriminatorSchema_SerializerAnnotationsDeclareKnownTypes(SerializerLibraries serializerLibraries)
        {
            var discriminator = SchemaTestHelper.CreateDiscriminatorSchema("kind");
            var derivedSchemas = SchemaTestHelper.CreateDerivedSchemas("kind", baseSchema: discriminator, "Dog", "Cat");
            var oneOfSchema = SchemaTestHelper.CreateOneOfSchema(derivedSchemas.Select(x => x.Value), optional: true);
            var schema = SchemaTestHelper.CreateContainerSchema(derivedSchemas.Append(new("Animal", discriminator)));
            schema.Definitions.Add("AnimalTypes", oneOfSchema);
            schema.Properties.Add("Animals", new()
            {
                Type = JsonObjectType.Array,
                Item = oneOfSchema
            });

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
        public void GenerateFromOneOfDiscriminatorSchemaProperty_SerializerAnnotationsDeclareKnownTypes(SerializerLibraries serializerLibraries)
        {
            var derivedSchemas = SchemaTestHelper.CreateDerivedSchemas("kind", "Dog", "Cat");
            var discriminator = SchemaTestHelper.CreateDiscriminatorSchema<JsonSchemaProperty>("kind", derivedSchemas);
            var schema = SchemaTestHelper.CreateContainerSchema(derivedSchemas);
            schema.Properties.Add("Animal", discriminator);

            var generator = TestHelper.CreateGenerator(schema, serializerLibraries);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("class Dog : Animal"), "Derived types do not inherit from base type.");
            Assert.IsTrue(!code.Contains("public enum DogKind"), "Discriminator property is repeated in derived types.");
            Assert.IsTrue(code.Contains("Animal Animal"), "Container element type does not match base type.");
            Assert.IsTrue(code.Contains("[JsonInheritanceAttribute(\"Dog\", typeof(Dog))]"));
            AssertDiscriminatorAttribute(code, serializerLibraries, "kind");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        [DataRow(SerializerLibraries.YamlDotNet)]
        [DataRow(SerializerLibraries.NewtonsoftJson)]
        [DataRow(SerializerLibraries.NewtonsoftJson | SerializerLibraries.YamlDotNet)]
        public void GenerateFromOneOfDiscriminatorRefSchemaProperty_SerializerAnnotationsDeclareKnownTypes(SerializerLibraries serializerLibraries)
        {
            var derivedSchemas = SchemaTestHelper.CreateDerivedSchemas("kind", "Dog", "Cat");
            var discriminator = SchemaTestHelper.CreateDiscriminatorSchema("kind", derivedSchemas);
            var schema = SchemaTestHelper.CreateContainerSchema(derivedSchemas.Prepend(new("Animal", discriminator)));
            schema.Properties.Add("Animal", new JsonSchemaProperty { Reference = discriminator });

            var generator = TestHelper.CreateGenerator(schema, serializerLibraries);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("class Dog : Animal"), "Derived types do not inherit from base type.");
            Assert.IsTrue(!code.Contains("public enum DogKind"), "Discriminator property is repeated in derived types.");
            Assert.IsTrue(code.Contains("Animal Animal"), "Container element type does not match base type.");
            Assert.IsTrue(code.Contains("[JsonInheritanceAttribute(\"Dog\", typeof(Dog))]"));
            AssertDiscriminatorAttribute(code, serializerLibraries, "kind");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        [DataRow(SerializerLibraries.YamlDotNet)]
        [DataRow(SerializerLibraries.NewtonsoftJson)]
        [DataRow(SerializerLibraries.NewtonsoftJson | SerializerLibraries.YamlDotNet)]
        public void GenerateFromArrayItemDiscriminator_EnsureFallbackDiscriminatorBaseTypeName(SerializerLibraries serializerLibraries)
        {
            var derivedSchemas = SchemaTestHelper.CreateDerivedSchemas("kind", "Dog", "Cat");
            var discriminator = SchemaTestHelper.CreateDiscriminatorSchema("kind", derivedSchemas);
            var schema = SchemaTestHelper.CreateContainerSchema(derivedSchemas);
            schema.Properties.Add("Animals", new()
            {
                Type = JsonObjectType.Array,
                Item = discriminator
            });

            var generator = TestHelper.CreateGenerator(schema, serializerLibraries);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("class Dog : Anonymous"), "Derived types do not inherit from base type.");
            Assert.IsTrue(!code.Contains("public enum DogKind"), "Discriminator property is repeated in derived types.");
            Assert.IsTrue(code.Contains("List<Anonymous> Animal"), "Container element type does not match base type.");
            Assert.IsTrue(code.Contains("[JsonInheritanceAttribute(\"Dog\", typeof(Dog))]"));
            AssertDiscriminatorAttribute(code, serializerLibraries, "kind");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        [DataRow(SerializerLibraries.YamlDotNet)]
        [DataRow(SerializerLibraries.NewtonsoftJson)]
        [DataRow(SerializerLibraries.NewtonsoftJson | SerializerLibraries.YamlDotNet)]
        public void GenerateFromArrayItemDiscriminatorRef_EnsureFallbackDiscriminatorBaseTypeName(SerializerLibraries serializerLibraries)
        {
            var derivedSchemas = SchemaTestHelper.CreateDerivedSchemas("kind", "Dog", "Cat");
            var discriminator = SchemaTestHelper.CreateDiscriminatorSchema("kind", derivedSchemas);
            var schema = SchemaTestHelper.CreateContainerSchema(derivedSchemas.Prepend(new("Animal", discriminator)));
            schema.Properties.Add("Animals", new()
            {
                Type = JsonObjectType.Array,
                Item = new() { Reference = discriminator }
            });

            var generator = TestHelper.CreateGenerator(schema, serializerLibraries);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("class Dog : Animal"), "Derived types do not inherit from base type.");
            Assert.IsTrue(!code.Contains("public enum DogKind"), "Discriminator property is repeated in derived types.");
            Assert.IsTrue(code.Contains("List<Animal> Animals"), "Container element type does not match base type.");
            Assert.IsTrue(code.Contains("[JsonInheritanceAttribute(\"Dog\", typeof(Dog))]"));
            AssertDiscriminatorAttribute(code, serializerLibraries, "kind");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        [DataRow(SerializerLibraries.YamlDotNet)]
        [DataRow(SerializerLibraries.NewtonsoftJson)]
        [DataRow(SerializerLibraries.NewtonsoftJson | SerializerLibraries.YamlDotNet)]
        public void GenerateFromSubDiscriminatorSchemas_InheritanceHierarchyIsPreserved(SerializerLibraries serializerLibraries)
        {
            var subTypeSchemas = SchemaTestHelper.CreateDerivedSchemas("fur", "long", "short");
            var subDiscriminator = SchemaTestHelper.CreateDiscriminatorSchema("fur", subTypeSchemas);
            var derivedSchemas = SchemaTestHelper.CreateDerivedSchemas("type", "cat", "dog");
            derivedSchemas[0].Value.DiscriminatorObject = subDiscriminator.DiscriminatorObject;
            foreach (var subSchema in subDiscriminator.OneOf)
            {
                derivedSchemas[0].Value.OneOf.Add(subSchema);
            }
            var discriminator = SchemaTestHelper.CreateDiscriminatorSchema("type", derivedSchemas);
            var schema = SchemaTestHelper.CreateContainerSchema(new Dictionary<string, JsonSchema>
            {
                { "LongFurCat", subTypeSchemas[0].Value },
                { "Cat", derivedSchemas[0].Value },
                { "Dog", derivedSchemas[1].Value },
                { "Animal", discriminator },
                { "ShortFurCat", subTypeSchemas[1].Value }
            });
            schema.Properties.Add("Animals", new()
            {
                Type = JsonObjectType.Array,
                Item = new() { Reference = discriminator }
            });

            var generator = TestHelper.CreateGenerator(schema, serializerLibraries);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("class Cat : Animal"), "Derived types do not inherit from base type.");
            Assert.IsTrue(!code.Contains("public enum LongFurCatFur"), "Discriminator property is repeated in derived types.");
            Assert.IsTrue(code.Contains("List<Animal> Animals"), "Container element type does not match base type.");
            Assert.IsTrue(code.Contains("[JsonInheritanceAttribute(\"dog\", typeof(Dog))]"));
            AssertDiscriminatorAttribute(code, serializerLibraries, "type");
            CompilerTestHelper.CompileFromSource(code);
        }
    }
}
