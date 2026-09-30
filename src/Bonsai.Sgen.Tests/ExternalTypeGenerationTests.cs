using Microsoft.VisualStudio.TestTools.UnitTesting;
using NJsonSchema;

namespace Bonsai.Sgen.Tests
{
    [TestClass]
    public class ExternalTypeGenerationTests
    {
        private static async Task<JsonSchema> CreateCommonDefinitions()
        {
            return await SchemaTestHelper.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""definitions"": {
      ""CommonType"": {
        ""type"": ""object"",
        ""properties"": {
          ""Bar"": {
            ""type"": ""integer""
          }
        }
      }
    }
  }
");
        }

        [TestMethod]
        public void GenerateWithEmptyDefinitions_EmitEmptyCodeBlock()
        {
            var schema = new JsonSchema();
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateWithDefinitionsOnly_EmitDefinitionTypes()
        {
            var schema = await CreateCommonDefinitions();
            var generator = TestHelper.CreateGenerator(schema);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("public partial class CommonType"), "Missing type definition.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateWithExternalTypeReferenceProperty_OmitExternalTypeDefinition()
        {
            var schemaA = await CreateCommonDefinitions();
            var generatorA = TestHelper.CreateGenerator(schemaA, schemaNamespace: $"{nameof(TestHelper)}.Base");
            var codeA = generatorA.GenerateFile();

            var schemaB = await SchemaTestHelper.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""definitions"": {
      ""SpecificType"": {
        ""type"": ""object"",
        ""properties"": {
          ""Bar"": {
            ""$ref"": ""https://schemaA/definitions/CommonType""
          },
          ""Baz"": {
            ""type"": ""integer""
          }
        }
      }
    }
  }
",
documentPath: "",
schema => new TestJsonReferenceResolver(
    new JsonSchemaAppender(schema, new DefaultTypeNameGenerator()),
    schemaA,
    generatorA.Settings.Namespace));

            var generatorB = TestHelper.CreateGenerator(schemaB, schemaNamespace: $"{nameof(TestHelper)}.Derived");
            var codeB = generatorB.GenerateFile();
            Assert.IsTrue(codeB.Contains("public TestHelper.Base.CommonType Bar"), "Incorrect type declaration.");
            CompilerTestHelper.CompileFromSource(codeA, codeB);
        }

        [TestMethod]
        public async Task GenerateWithExternalBaseTypeReference_OmitExternalTypeDefinition()
        {
            var schemaA = await CreateCommonDefinitions();
            var generatorA = TestHelper.CreateGenerator(schemaA, schemaNamespace: $"{nameof(TestHelper)}.Base");
            var codeA = generatorA.GenerateFile();

            var schemaB = await SchemaTestHelper.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""definitions"": {
      ""DerivedType"": {
        ""type"": ""object"",
        ""properties"": {
          ""Baz"": {
            ""type"": [
              ""null"",
              ""integer""
            ]
          }
        },
        ""allOf"": [
          {
            ""$ref"": ""https://schemaA/definitions/CommonType""
          }
        ]
      }
    }
  }
",
documentPath: "",
schema => new TestJsonReferenceResolver(
    new JsonSchemaAppender(schema, new DefaultTypeNameGenerator()),
    schemaA,
    generatorA.Settings.Namespace));

            var generatorB = TestHelper.CreateGenerator(schemaB, schemaNamespace: $"{nameof(TestHelper)}.Derived");
            var codeB = generatorB.GenerateFile();
            Assert.IsTrue(codeB.Contains("class DerivedType : TestHelper.Base.CommonType"), "Incorrect type declaration.");
            CompilerTestHelper.CompileFromSource(codeA, codeB);
        }

        [TestMethod]
        public async Task GenerateWithInternalTypeReference_EmitInternalTypeDefinition()
        {
            var schemaNamespace = $"{nameof(TestHelper)}.Derived";
            var schemaA = await CreateCommonDefinitions();
            var schemaB = await SchemaTestHelper.FromJsonAsync(@"
{
    ""$schema"": ""http://json-schema.org/draft-04/schema#"",
    ""definitions"": {
      ""SpecificType"": {
        ""type"": ""object"",
        ""properties"": {
          ""Bar"": {
            ""$ref"": ""https://schemaA/definitions/CommonType""
          },
          ""Baz"": {
            ""type"": ""integer""
          }
        }
      }
    }
  }
",
            documentPath: "",
            schema => new TestJsonReferenceResolver(
                new JsonSchemaAppender(schema, new DefaultTypeNameGenerator()),
                schemaA,
                schemaNamespace));

            var generatorB = TestHelper.CreateGenerator(schemaB, schemaNamespace: schemaNamespace);
            var codeB = generatorB.GenerateFile();
            Assert.IsTrue(codeB.Contains("public partial class CommonType"), "Missing internal type definition.");
            CompilerTestHelper.CompileFromSource(codeB);
        }

        [TestMethod]
        public void GenerateWithExternalDiscriminatorReferenceProperty_OmitExternalDiscriminatorDefinition()
        {
            var derivedSchemas = SchemaTestHelper.CreateDerivedSchemas("kind", "Dog", "Cat");
            var discriminator = SchemaTestHelper.CreateDiscriminatorSchema<JsonSchemaProperty>("kind", derivedSchemas);
            var schema = SchemaTestHelper.CreateContainerSchema(derivedSchemas);
            schema.Properties.Add("Animal", discriminator);

            var schemaProperty = schema.Properties.First();
            foreach (var definition in derivedSchemas.Prepend(new(schemaProperty.Key, schemaProperty.Value)))
            {
                definition.Value.ExtensionData ??= new Dictionary<string, object>();
                definition.Value.ExtensionData[JsonSchemaExtensions.TypeNameAnnotation] = $"TestHelper.Base.{definition.Key}";
            }

            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: nameof(TestHelper) + ".Derived");
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("public TestHelper.Base.Animal Animal"), "Container must reference external type.");
            Assert.IsTrue(!code.Contains("public partial class Animal"), "External discriminator base type should not be generated.");

            const string externalCode = @"
            namespace TestHelper.Base
            {
                public class Animal { }
                public class Dog : Animal { }
                public class Cat : Animal { }
            }
            ";
            CompilerTestHelper.CompileFromSource(externalCode, code);
        }
    }
}
