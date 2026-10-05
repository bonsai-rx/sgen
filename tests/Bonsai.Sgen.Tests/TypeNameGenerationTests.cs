using Microsoft.VisualStudio.TestTools.UnitTesting;
using NJsonSchema;

namespace Bonsai.Sgen.Tests
{
    [TestClass]
    public class TypeNameGenerationTests
    {
        const string SchemaNamespace = $"{nameof(TestHelper)}.Derived";

        [TestMethod]
        public async Task GenerateWithInternalTypeNameAnnotation_UseAnnotatedTypeName()
        {
            var schema = await SchemaTestHelper.FromJsonAsync(@"
{
  ""$defs"": {
    ""SomeOtherKey"": {
      ""type"": ""object"",
      ""title"": ""Other"",
      ""properties"": { ""age"": { ""type"": ""integer"" } },
      ""x-sgen-typename"": ""TestHelper.Derived.Dog""
    }
  },
  ""type"": ""object"",
  ""title"": ""Container"",
  ""properties"": { ""local"": { ""$ref"": ""#/$defs/SomeOtherKey"" } }
}");
            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("public partial class Dog"), "Missing annotated type definition.");
            Assert.IsTrue(code.Contains("public Dog Local"), "Property must reference annotated type.");
            Assert.IsFalse(code.Contains("SomeOtherKey"), "Definition key must not be used as type name.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateWithInternalAndExternalTypesSharingName_UseAnnotatedTypeNames()
        {
            // Pydantic makes definition keys unique by prefixing the module name whenever two
            // models share a plain name, so the local type must be named from its annotation.
            var schema = await SchemaTestHelper.FromJsonAsync(@"
{
  ""$defs"": {
    ""base__Dog"": {
      ""type"": ""object"",
      ""title"": ""Dog"",
      ""properties"": { ""name"": { ""type"": ""string"" } },
      ""x-sgen-typename"": ""TestHelper.Base.Dog""
    },
    ""derived__Dog"": {
      ""type"": ""object"",
      ""title"": ""Dog"",
      ""properties"": { ""age"": { ""type"": ""integer"" } },
      ""x-sgen-typename"": ""TestHelper.Derived.Dog""
    }
  },
  ""type"": ""object"",
  ""title"": ""Container"",
  ""properties"": {
    ""external"": { ""$ref"": ""#/$defs/base__Dog"" },
    ""local"": { ""$ref"": ""#/$defs/derived__Dog"" }
  }
}");
            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("public partial class Dog"), "Missing annotated type definition.");
            Assert.IsTrue(code.Contains("public TestHelper.Base.Dog External"), "Property must reference external type.");
            Assert.IsTrue(code.Contains("public Dog Local"), "Property must reference annotated type.");
            Assert.IsFalse(code.Contains("DerivedDog"), "Definition key must not be used as type name.");

            const string externalCode = @"
            namespace TestHelper.Base
            {
                public class Dog { }
            }
            ";
            CompilerTestHelper.CompileFromSource(externalCode, code);
        }

        [TestMethod]
        public async Task GenerateWithDefinitionKeyMatchingAnnotatedTypeName_ReserveAnnotatedTypeName()
        {
            // The definition without annotation is registered first, so the annotated type name
            // must be reserved before any type is named.
            var schema = await SchemaTestHelper.FromJsonAsync(@"
{
  ""$defs"": {
    ""Dog"": {
      ""type"": ""object"",
      ""properties"": { ""name"": { ""type"": ""string"" } }
    },
    ""Other"": {
      ""type"": ""object"",
      ""properties"": { ""age"": { ""type"": ""integer"" } },
      ""x-sgen-typename"": ""TestHelper.Derived.Dog""
    }
  },
  ""type"": ""object"",
  ""title"": ""Container"",
  ""properties"": {
    ""plain"": { ""$ref"": ""#/$defs/Dog"" },
    ""annotated"": { ""$ref"": ""#/$defs/Other"" }
  }
}");
            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("public Dog Annotated"), "Annotated type must keep its annotated name.");
            Assert.IsTrue(code.Contains("public Dog2 Plain"), "Type without annotation must be renamed.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public void GenerateWithAnnotatedInlineDiscriminator_UseAnnotatedTypeName()
        {
            // The synthesized discriminator base type copies the annotation of the inline schema,
            // so two schemas carry the same annotation without clashing.
            var derivedSchemas = SchemaTestHelper.CreateDerivedSchemas("kind", "Dog", "Cat");
            var discriminator = SchemaTestHelper.CreateDiscriminatorSchema<JsonSchemaProperty>("kind", derivedSchemas);
            discriminator.ExtensionData = new Dictionary<string, object>
            {
                [JsonSchemaExtensions.TypeNameAnnotation] = $"{SchemaNamespace}.Animal"
            };
            var schema = SchemaTestHelper.CreateContainerSchema(derivedSchemas);
            schema.Properties.Add("Pet", discriminator);

            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("class Dog : Animal"), "Derived types must inherit from annotated base type.");
            Assert.IsTrue(code.Contains("public Animal Pet"), "Property must reference annotated base type.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateWithAnnotatedDiscriminatorRefSharedByProperties_UseAnnotatedTypeName()
        {
            // Every reference to the union must resolve to the same definition, otherwise each
            // property synthesizes its own base type carrying the same annotation.
            var schema = await SchemaTestHelper.FromJsonAsync(@"
{
  ""$defs"": {
    ""Cat"": {
      ""properties"": { ""kind"": { ""const"": ""cat"", ""default"": ""cat"", ""type"": ""string"" } },
      ""title"": ""Cat"",
      ""type"": ""object"",
      ""x-sgen-typename"": ""TestHelper.Derived.Cat""
    },
    ""Dog"": {
      ""properties"": { ""kind"": { ""const"": ""dog"", ""default"": ""dog"", ""type"": ""string"" } },
      ""title"": ""Dog"",
      ""type"": ""object"",
      ""x-sgen-typename"": ""TestHelper.Derived.Dog""
    },
    ""derived__Animal"": {
      ""discriminator"": {
        ""mapping"": { ""cat"": ""#/$defs/Cat"", ""dog"": ""#/$defs/Dog"" },
        ""propertyName"": ""kind""
      },
      ""oneOf"": [ { ""$ref"": ""#/$defs/Cat"" }, { ""$ref"": ""#/$defs/Dog"" } ],
      ""title"": ""Animal"",
      ""x-sgen-typename"": ""TestHelper.Derived.Animal""
    }
  },
  ""type"": ""object"",
  ""title"": ""Container"",
  ""properties"": {
    ""pet"": { ""$ref"": ""#/$defs/derived__Animal"" },
    ""stray"": { ""$ref"": ""#/$defs/derived__Animal"" }
  }
}");
            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("class Dog : Animal"), "Derived types must inherit from annotated base type.");
            Assert.IsTrue(code.Contains("public Animal Pet"), "First property must reference annotated base type.");
            Assert.IsTrue(code.Contains("public Animal Stray"), "Second property must reference annotated base type.");
            Assert.IsFalse(code.Contains("class Pet") || code.Contains("class Stray"), "Unexpected base type generated for property.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateWithDuplicateTypeNameAnnotations_ThrowsNamingBothDefinitions()
        {
            var schema = await SchemaTestHelper.FromJsonAsync(@"
{
  ""$defs"": {
    ""first__Dog"": {
      ""type"": ""object"",
      ""properties"": { ""name"": { ""type"": ""string"" } },
      ""x-sgen-typename"": ""TestHelper.Derived.Dog""
    },
    ""second__Dog"": {
      ""type"": ""object"",
      ""properties"": { ""age"": { ""type"": ""integer"" } },
      ""x-sgen-typename"": ""TestHelper.Derived.Dog""
    }
  },
  ""type"": ""object"",
  ""title"": ""Container"",
  ""properties"": {
    ""first"": { ""$ref"": ""#/$defs/first__Dog"" },
    ""second"": { ""$ref"": ""#/$defs/second__Dog"" }
  }
}");
            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var exception = Assert.ThrowsException<InvalidOperationException>(() => generator.GenerateFile());
            StringAssert.Contains(exception.Message, "first__Dog");
            StringAssert.Contains(exception.Message, "second__Dog");
        }
    }
}
