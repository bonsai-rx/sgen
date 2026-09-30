using Microsoft.VisualStudio.TestTools.UnitTesting;
using NJsonSchema;

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
            var schema = await SchemaTestHelper.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""type"": ""object"",
    ""title"": ""Container"",
    ""additionalProperties"": false,
    ""properties"": {
      ""Animal"": {
        ""oneOf"": [
          {
            ""$ref"": ""#/$defs/Animal""
          },
          {
            ""type"": ""null""
          }
        ]
      }
    },
    ""$defs"": {
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
            ""$ref"": ""#/$defs/Animal""
          }
        ]
      },
      ""Animal"": {
        ""type"": ""object"",
        ""discriminator"": {
          ""propertyName"": ""discriminator"",
          ""mapping"": {
              ""DogType"": ""#/$defs/Dog""
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
        public async Task GenerateFromDiscriminatorRefSharedByProperties_OmitPropertyBaseTypes(SerializerLibraries serializerLibraries)
        {
            var schema = await SchemaTestHelper.FromJsonAsync(@"
{
  ""$defs"": {
    ""Cat"": {
      ""properties"": { ""kind"": { ""const"": ""cat"", ""default"": ""cat"", ""type"": ""string"" } },
      ""title"": ""Cat"",
      ""type"": ""object""
    },
    ""Dog"": {
      ""properties"": { ""kind"": { ""const"": ""dog"", ""default"": ""dog"", ""type"": ""string"" } },
      ""title"": ""Dog"",
      ""type"": ""object""
    },
    ""Animal"": {
      ""discriminator"": {
        ""mapping"": { ""cat"": ""#/$defs/Cat"", ""dog"": ""#/$defs/Dog"" },
        ""propertyName"": ""kind""
      },
      ""oneOf"": [ { ""$ref"": ""#/$defs/Cat"" }, { ""$ref"": ""#/$defs/Dog"" } ],
      ""title"": ""Animal""
    }
  },
  ""properties"": {
    ""pet"": { ""$ref"": ""#/$defs/Animal"" },
    ""stray"": { ""$ref"": ""#/$defs/Animal"" }
  },
  ""title"": ""Container"",
  ""type"": ""object""
}
");
            var generator = TestHelper.CreateGenerator(schema, serializerLibraries);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("class Dog : Animal"), "Derived types do not inherit from base type.");
            Assert.IsTrue(code.Contains("public Animal Pet"), "First property does not reference base type.");
            Assert.IsTrue(code.Contains("public Animal Stray"), "Second property does not reference base type.");
            Assert.IsFalse(code.Contains("class Pet"), "Unexpected base type generated for first property.");
            Assert.IsFalse(code.Contains("class Stray"), "Unexpected base type generated for second property.");
            Assert.IsFalse(code.Contains("class MatchPet") || code.Contains("class MatchStray"), "Unexpected match operator generated for property.");
            AssertDiscriminatorAttribute(code, serializerLibraries, "kind");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateFromExternalDiscriminatorRefSharedByProperties_OmitPropertyBaseTypes()
        {
            var directory = Directory.CreateTempSubdirectory(nameof(DiscriminatorGenerationTests));
            try
            {
                File.WriteAllText(Path.Combine(directory.FullName, "animals.json"), @"
{
  ""$defs"": {
    ""Cat"": {
      ""properties"": { ""kind"": { ""const"": ""cat"", ""default"": ""cat"", ""type"": ""string"" } },
      ""title"": ""Cat"",
      ""type"": ""object""
    },
    ""Dog"": {
      ""properties"": { ""kind"": { ""const"": ""dog"", ""default"": ""dog"", ""type"": ""string"" } },
      ""title"": ""Dog"",
      ""type"": ""object""
    },
    ""Animal"": {
      ""discriminator"": {
        ""mapping"": { ""cat"": ""#/$defs/Cat"", ""dog"": ""#/$defs/Dog"" },
        ""propertyName"": ""kind""
      },
      ""oneOf"": [ { ""$ref"": ""#/$defs/Cat"" }, { ""$ref"": ""#/$defs/Dog"" } ],
      ""title"": ""Animal""
    }
  }
}
");
                var containerPath = Path.Combine(directory.FullName, "container.json");
                File.WriteAllText(containerPath, @"
{
  ""properties"": {
    ""pet"": { ""$ref"": ""animals.json#/$defs/Animal"" },
    ""stray"": { ""$ref"": ""animals.json#/$defs/Animal"" }
  },
  ""title"": ""Container"",
  ""type"": ""object""
}
");
                var schema = await SchemaTestHelper.FromFileAsync(containerPath);
                var generator = TestHelper.CreateGenerator(schema);
                var code = generator.GenerateFile();
                Assert.IsTrue(code.Contains("class Dog : Animal"), "Derived types do not inherit from base type.");
                Assert.IsTrue(code.Contains("public Animal Pet"), "First property does not reference base type.");
                Assert.IsTrue(code.Contains("public Animal Stray"), "Second property does not reference base type.");
                Assert.IsFalse(code.Contains("class Pet") || code.Contains("class Stray"), "Unexpected base type generated for property.");
                CompilerTestHelper.CompileFromSource(code);
            }
            finally
            {
                try { directory.Delete(recursive: true); } catch (IOException) { }
            }
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
