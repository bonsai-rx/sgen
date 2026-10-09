using System.Linq.Expressions;
using System.Reactive.Linq;
using Bonsai.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NJsonSchema;

namespace Bonsai.Sgen.Tests
{
    [TestClass]
    public class UnionWrapperGenerationTests
    {
        const string SchemaNamespace = $"{nameof(TestHelper)}.Derived";

        const string ExternalCode = @"
namespace TestHelper.Base
{
    public class Hamster
    {
        public Hamster()
        {
            Kind = ""hamster"";
        }

        [Newtonsoft.Json.JsonPropertyAttribute(""kind"")]
        [YamlDotNet.Serialization.YamlMemberAttribute(Alias=""kind"")]
        public string Kind { get; set; }

        [Newtonsoft.Json.JsonPropertyAttribute(""name"")]
        [YamlDotNet.Serialization.YamlMemberAttribute(Alias=""name"")]
        public string Name { get; set; }
    }
}
";

        static Task<JsonSchema> CreateExternalMemberSchema()
        {
            return SchemaTestHelper.FromJsonAsync(@"
{
  ""$defs"": {
    ""Hamster"": {
      ""properties"": {
        ""kind"": { ""const"": ""hamster"", ""default"": ""hamster"", ""type"": ""string"" },
        ""name"": { ""type"": ""string"" }
      },
      ""title"": ""Hamster"",
      ""type"": ""object"",
      ""x-sgen-typename"": ""TestHelper.Base.Hamster""
    },
    ""Dog"": {
      ""properties"": {
        ""kind"": { ""const"": ""dog"", ""default"": ""dog"", ""type"": ""string"" },
        ""name"": { ""type"": ""string"" }
      },
      ""title"": ""Dog"",
      ""type"": ""object""
    },
    ""Animal"": {
      ""discriminator"": {
        ""mapping"": { ""dog"": ""#/$defs/Dog"", ""hamster"": ""#/$defs/Hamster"" },
        ""propertyName"": ""kind""
      },
      ""oneOf"": [ { ""$ref"": ""#/$defs/Dog"" }, { ""$ref"": ""#/$defs/Hamster"" } ],
      ""title"": ""Animal""
    }
  },
  ""properties"": { ""pet"": { ""$ref"": ""#/$defs/Animal"" } },
  ""title"": ""Container"",
  ""type"": ""object""
}
");
        }

        static Task<JsonSchema> CreateSharedMemberSchema()
        {
            return SchemaTestHelper.FromJsonAsync(@"
{
  ""$defs"": {
    ""Cat"": {
      ""properties"": {
        ""kind"": { ""const"": ""cat"", ""default"": ""cat"", ""type"": ""string"" },
        ""name"": { ""type"": ""string"" }
      },
      ""title"": ""Cat"",
      ""type"": ""object""
    },
    ""Dog"": {
      ""properties"": {
        ""kind"": { ""const"": ""dog"", ""default"": ""dog"", ""type"": ""string"" },
        ""name"": { ""type"": ""string"" }
      },
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
    },
    ""Goose"": {
      ""properties"": {
        ""kind"": { ""const"": ""goose"", ""default"": ""goose"", ""type"": ""string"" },
        ""name"": { ""type"": ""string"" }
      },
      ""title"": ""Goose"",
      ""type"": ""object""
    },
    ""Guard"": {
      ""discriminator"": {
        ""mapping"": { ""dog"": ""#/$defs/Dog"", ""goose"": ""#/$defs/Goose"" },
        ""propertyName"": ""kind""
      },
      ""oneOf"": [ { ""$ref"": ""#/$defs/Dog"" }, { ""$ref"": ""#/$defs/Goose"" } ],
      ""title"": ""Guard""
    }
  },
  ""properties"": {
    ""pet"": { ""$ref"": ""#/$defs/Animal"" },
    ""guard"": { ""$ref"": ""#/$defs/Guard"" }
  },
  ""title"": ""Container"",
  ""type"": ""object""
}
");
        }

        static JsonSchema CreateInheritedMemberSchema()
        {
            var mammal = new JsonSchema
            {
                Type = JsonObjectType.Object,
                Properties = { { "age", new JsonSchemaProperty { Type = JsonObjectType.Integer } } }
            };
            var members = SchemaTestHelper.CreateDerivedSchemas("kind", "Cat")
                .Concat(SchemaTestHelper.CreateDerivedSchemas("kind", baseSchema: mammal, "Dog"))
                .ToArray();
            var animal = SchemaTestHelper.CreateDiscriminatorSchema("kind", members);
            var schema = SchemaTestHelper.CreateContainerSchema(
                members.Prepend(new("Mammal", mammal)).Prepend(new("Animal", animal)));
            schema.Properties.Add("pet", new JsonSchemaProperty { Reference = animal });
            return schema;
        }

        static JsonSchema CreateCommonBaseMemberSchema(bool unionInheritsBase)
        {
            var mammal = new JsonSchema
            {
                Type = JsonObjectType.Object,
                Properties = { { "age", new JsonSchemaProperty { Type = JsonObjectType.Integer } } }
            };
            var members = SchemaTestHelper.CreateDerivedSchemas("kind", baseSchema: mammal, "Cat", "Dog");
            var animal = SchemaTestHelper.CreateDiscriminatorSchema("kind", members);
            if (unionInheritsBase) animal.AllOf.Add(new JsonSchema { Reference = mammal });
            var schema = SchemaTestHelper.CreateContainerSchema(
                members.Prepend(new("Mammal", mammal)).Prepend(new("Animal", animal)));
            schema.Properties.Add("pet", new JsonSchemaProperty { Reference = animal });
            return schema;
        }

        [TestMethod]
        public async Task GenerateWithExternalUnionMember_GenerateWrapperType()
        {
            var schema = await CreateExternalMemberSchema();
            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("class Dog : Animal"), "Local member must inherit from union base type.");
            Assert.IsTrue(code.Contains("internal sealed partial class AnimalHamster : Animal, IUnionWrapper"), "Missing internal wrapper type for external member.");
            Assert.IsTrue(code.Contains("public interface IUnionWrapper<out T>"), "Wrapped values must be reachable through a public covariant interface.");
            Assert.IsFalse(code.Contains("public AnimalHamster()"), "Wrapper type must not be constructible as a workflow element.");
            Assert.IsTrue(code.Contains("public static implicit operator Animal(TestHelper.Base.Hamster value)"), "Missing conversion from external member to union.");
            Assert.IsTrue(code.Contains("TypeMapping<TestHelper.Base.Hamster>"), "Match operator must offer external member by its own type.");
            Assert.IsFalse(code.Contains("TypeMapping<AnimalHamster>"), "Deserializers must not offer wrapper type.");
            Assert.IsFalse(code.Contains("IObservable<AnimalHamster>"), "Serializers must not offer wrapper type.");
            Assert.IsFalse(code.Contains("public partial class Hamster"), "External member must not be generated.");
            CompilerTestHelper.CompileFromSource(ExternalCode, code);
        }

        [TestMethod]
        [DataRow(SerializerLibraries.NewtonsoftJson, "Json", @"{""pet"":{""kind"":""hamster"",""name"":""Sid""}}")]
        [DataRow(SerializerLibraries.YamlDotNet, "Yaml", "pet:\n  kind: hamster\n  name: Sid\n")]
        public async Task GenerateWithExternalUnionMember_RoundTripFlatFormat(SerializerLibraries serializerLibraries, string format, string text)
        {
            var schema = await CreateExternalMemberSchema();
            var generator = TestHelper.CreateGenerator(schema, serializerLibraries, SchemaNamespace);
            var assembly = CompilerTestHelper.CompileToAssembly(ExternalCode, generator.GenerateFile());
            var containerType = SerializerTestHelper.GetGeneratedType(assembly, "Container");
            var container = await SerializerTestHelper.Deserialize(assembly, $"DeserializeFrom{format}", containerType, text);
            var member = await WorkflowTestHelper.Select(container, "Pet");
            Assert.AreEqual("AnimalHamster", member.GetType().Name, "Tagged external member must deserialize as wrapper type.");
            Assert.AreEqual("Sid", await WorkflowTestHelper.Select(member, "Value.Name"), "Wrapper value must carry member properties.");

            var output = await SerializerTestHelper.Serialize(assembly, $"SerializeTo{format}", containerType, container);
            StringAssert.Contains(output, "hamster", "Serialized wrapper must carry its tag.");
            StringAssert.Contains(output, "Sid", "Serialized wrapper must carry member properties.");
            Assert.IsFalse(output.Contains("Value"), "Serialized wrapper must use the flat format.");
        }

        [TestMethod]
        public async Task GenerateWithExternalUnionMember_MatchOperatorSelectsWrappedValue()
        {
            var schema = await CreateExternalMemberSchema();
            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var assembly = CompilerTestHelper.CompileToAssembly(ExternalCode, generator.GenerateFile());
            var externalType = SerializerTestHelper.GetGeneratedType(assembly, "Hamster");
            var unionType = SerializerTestHelper.GetGeneratedType(assembly, "Animal");
            var match = (ExpressionBuilder)Activator.CreateInstance(SerializerTestHelper.GetGeneratedType(assembly, "MatchAnimal"))!;
            WorkflowTestHelper.SetTypeMapping(match, "Hamster");

            var source = WorkflowTestHelper.Return(unionType, Expression.New(externalType));
            var result = await WorkflowTestHelper.BuildObservable<object>(source, match);
            Assert.AreEqual(externalType, result.GetType(), "Match operator must select the wrapped value.");
        }

        [TestMethod]
        public async Task GenerateWithExternalUnionMember_MatchOperatorConvertsMemberToUnion()
        {
            var schema = await CreateExternalMemberSchema();
            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var assembly = CompilerTestHelper.CompileToAssembly(ExternalCode, generator.GenerateFile());
            var externalType = SerializerTestHelper.GetGeneratedType(assembly, "Hamster");
            var localType = SerializerTestHelper.GetGeneratedType(assembly, "Dog");
            var matchType = SerializerTestHelper.GetGeneratedType(assembly, "MatchAnimal");

            var externalSource = WorkflowTestHelper.Return(externalType, Expression.New(externalType));
            var externalMatch = (ExpressionBuilder)Activator.CreateInstance(matchType)!;
            var externalResult = await WorkflowTestHelper.BuildObservable<object>(externalSource, externalMatch);
            Assert.AreEqual("AnimalHamster", externalResult.GetType().Name, "Match operator with no type must convert external member to union.");

            var localSource = WorkflowTestHelper.Return(localType, Expression.New(localType));
            var localMatch = (ExpressionBuilder)Activator.CreateInstance(matchType)!;
            var localResult = await WorkflowTestHelper.BuildObservable<object>(localSource, localMatch);
            Assert.AreEqual(localType, localResult.GetType(), "Match operator with no type must pass local member unchanged.");
        }

        [TestMethod]
        [DataRow("Dog", "Dog")]
        [DataRow("Hamster", "Hamster,Hamster")]
        [DataRow(null, "Dog,AnimalHamster")]
        public async Task GenerateWithExternalUnionMember_MatchOperatorFiltersObjectSequence(string? typeName, string expectedTypeNames)
        {
            var schema = await CreateExternalMemberSchema();
            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var assembly = CompilerTestHelper.CompileToAssembly(ExternalCode, generator.GenerateFile());
            var externalType = SerializerTestHelper.GetGeneratedType(assembly, "Hamster");
            var localType = SerializerTestHelper.GetGeneratedType(assembly, "Dog");
            var unionType = SerializerTestHelper.GetGeneratedType(assembly, "Animal");
            var match = (ExpressionBuilder)Activator.CreateInstance(SerializerTestHelper.GetGeneratedType(assembly, "MatchAnimal"))!;
            if (typeName != null) WorkflowTestHelper.SetTypeMapping(match, typeName);

            var source = WorkflowTestHelper.ToObservable(
                typeof(object),
                Expression.New(localType),
                Expression.Convert(Expression.New(externalType), unionType),
                Expression.New(externalType),
                Expression.Constant("unrelated"));
            var result = await WorkflowTestHelper.BuildObservable<object>(source, match).ToArray();
            Assert.AreEqual(expectedTypeNames, string.Join(",", result.Select(value => value.GetType().Name)), "Match operator must keep only the elements matching its type.");
        }

        [TestMethod]
        [DataRow("Dog", "Dog")]
        [DataRow("Hamster", "Hamster")]
        [DataRow(null, "Dog,AnimalHamster")]
        public async Task GenerateWithExternalUnionMember_MatchOperatorConvertsUnrelatedSequence(string? typeName, string expectedTypeNames)
        {
            const string PayloadCode = @"
namespace TestHelper.Base
{
    public class Payload
    {
        private TestHelper.Derived.Animal _value;

        public static implicit operator Payload(TestHelper.Derived.Animal value)
        {
            return new Payload { _value = value };
        }

        public static explicit operator TestHelper.Derived.Animal(Payload payload)
        {
            return payload._value;
        }
    }
}
";
            var schema = await CreateExternalMemberSchema();
            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var assembly = CompilerTestHelper.CompileToAssembly(ExternalCode, PayloadCode, generator.GenerateFile());
            var externalType = SerializerTestHelper.GetGeneratedType(assembly, "Hamster");
            var localType = SerializerTestHelper.GetGeneratedType(assembly, "Dog");
            var unionType = SerializerTestHelper.GetGeneratedType(assembly, "Animal");
            var payloadType = SerializerTestHelper.GetGeneratedType(assembly, "Payload");
            var match = (ExpressionBuilder)Activator.CreateInstance(SerializerTestHelper.GetGeneratedType(assembly, "MatchAnimal"))!;
            if (typeName != null) WorkflowTestHelper.SetTypeMapping(match, typeName);

            var source = WorkflowTestHelper.ToObservable(
                payloadType,
                Expression.Convert(Expression.New(localType), unionType),
                Expression.Convert(Expression.New(externalType), unionType));
            var result = await WorkflowTestHelper.BuildObservable<object>(source, match).ToArray();
            Assert.AreEqual(expectedTypeNames, string.Join(",", result.Select(value => value.GetType().Name)), "Match operator must convert an unrelated sequence to the union before matching.");
        }

        [TestMethod]
        [DataRow("Dog", "String")]
        [DataRow("Hamster", "Dog")]
        [DataRow("Hamster", "Int32")]
        public async Task GenerateWithExternalUnionMember_MatchOperatorRejectsImpossibleMatch(string typeName, string inputTypeName)
        {
            var schema = await CreateExternalMemberSchema();
            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var assembly = CompilerTestHelper.CompileToAssembly(ExternalCode, generator.GenerateFile());
            var inputType = inputTypeName switch
            {
                nameof(Int32) => typeof(int),
                nameof(String) => typeof(string),
                _ => SerializerTestHelper.GetGeneratedType(assembly, inputTypeName)
            };
            var match = (ExpressionBuilder)Activator.CreateInstance(SerializerTestHelper.GetGeneratedType(assembly, "MatchAnimal"))!;
            WorkflowTestHelper.SetTypeMapping(match, typeName);

            var source = WorkflowTestHelper.Return(inputType, Expression.Default(inputType));
            var exception = Assert.ThrowsException<WorkflowBuildException>(
                () => WorkflowTestHelper.BuildObservable<object>(source, match));
            StringAssert.Contains(exception.GetBaseException().Message, inputType.Name, "Match operator must reject an input that cannot match its type.");
        }

        [TestMethod]
        public async Task GenerateWithMemberSharedByUnions_WrapMemberInLaterUnion()
        {
            var schema = await CreateSharedMemberSchema();
            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("class Dog : Animal"), "Shared member must inherit from first union base type.");
            Assert.IsTrue(code.Contains("class GuardDog : Guard, IUnionWrapper"), "Missing wrapper type for shared member.");
            Assert.IsTrue(code.Contains("public Guard Guard"), "Second union property must reference its base type.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        public async Task GenerateWithUnion_UnionBaseIsAbstract()
        {
            var schema = await CreateSharedMemberSchema();
            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var assembly = CompilerTestHelper.CompileToAssembly(generator.GenerateFile());
            foreach (var typeName in new[] { "Animal", "Guard" })
            {
                var unionType = SerializerTestHelper.GetGeneratedType(assembly, typeName);
                Assert.IsTrue(unionType.IsAbstract, $"Union base {typeName} must be abstract.");
                Assert.IsFalse(Attribute.IsDefined(unionType, typeof(CombinatorAttribute)), $"Union base {typeName} must not be an operator.");
            }
        }

        [TestMethod]
        [DataRow(SerializerLibraries.NewtonsoftJson, "Json", @"{""pet"":{""kind"":""dog"",""name"":""Rex""},""guard"":{""kind"":""dog"",""name"":""Fang""}}")]
        [DataRow(SerializerLibraries.YamlDotNet, "Yaml", "pet:\n  kind: dog\n  name: Rex\nguard:\n  kind: dog\n  name: Fang\n")]
        public async Task GenerateWithMemberSharedByUnions_RoundTripFlatFormat(SerializerLibraries serializerLibraries, string format, string text)
        {
            var schema = await CreateSharedMemberSchema();
            var generator = TestHelper.CreateGenerator(schema, serializerLibraries, SchemaNamespace);
            var assembly = CompilerTestHelper.CompileToAssembly(generator.GenerateFile());
            var containerType = SerializerTestHelper.GetGeneratedType(assembly, "Container");
            var container = await SerializerTestHelper.Deserialize(assembly, $"DeserializeFrom{format}", containerType, text);
            Assert.AreEqual("Dog", (await WorkflowTestHelper.Select(container, "Pet")).GetType().Name);
            Assert.AreEqual("GuardDog", (await WorkflowTestHelper.Select(container, "Guard")).GetType().Name);

            var output = await SerializerTestHelper.Serialize(assembly, $"SerializeTo{format}", containerType, container);
            StringAssert.Contains(output, "Rex");
            StringAssert.Contains(output, "Fang");
            Assert.IsFalse(output.Contains("Value"), "Serialized wrapper must use the flat format.");
        }

        [TestMethod]
        public async Task GenerateWithExternalUnionOfUnions_ThrowsRecommendingSingleUnion()
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
    ""Bird"": {
      ""properties"": { ""kind"": { ""const"": ""bird"", ""default"": ""bird"", ""type"": ""string"" } },
      ""title"": ""Bird"",
      ""type"": ""object""
    },
    ""Pet"": {
      ""discriminator"": {
        ""mapping"": { ""bird"": ""#/$defs/Bird"", ""cat"": ""#/$defs/Cat"" },
        ""propertyName"": ""kind""
      },
      ""oneOf"": [ { ""$ref"": ""#/$defs/Bird"" }, { ""$ref"": ""#/$defs/Cat"" } ],
      ""title"": ""Pet"",
      ""x-sgen-typename"": ""TestHelper.Base.Pet""
    },
    ""Animal"": {
      ""discriminator"": {
        ""mapping"": { ""pet"": ""#/$defs/Pet"", ""dog"": ""#/$defs/Dog"" },
        ""propertyName"": ""kind""
      },
      ""oneOf"": [ { ""$ref"": ""#/$defs/Pet"" }, { ""$ref"": ""#/$defs/Dog"" } ],
      ""title"": ""Animal""
    }
  },
  ""properties"": { ""animal"": { ""$ref"": ""#/$defs/Animal"" } },
  ""title"": ""Container"",
  ""type"": ""object""
}
");
            var exception = Assert.ThrowsException<InvalidOperationException>(
                () => TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace).GenerateFile());
            StringAssert.Contains(exception.Message, "Animal");
            StringAssert.Contains(exception.Message, "Pet");
        }

        [TestMethod]
        public void GenerateWithMemberInheritingOtherType_WrapMember()
        {
            var schema = CreateInheritedMemberSchema();
            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("class Dog : Mammal"), "Member must keep its own base type.");
            Assert.IsTrue(code.Contains("class AnimalDog : Animal, IUnionWrapper"), "Missing wrapper type for member with its own base type.");
            Assert.IsTrue(code.Contains("[JsonInheritanceAttribute(\"Dog\", typeof(AnimalDog))]"), "Union must map the member tag to its wrapper type.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        [DataRow(SerializerLibraries.NewtonsoftJson, "Json", @"{""pet"":{""kind"":""Dog"",""age"":3}}")]
        [DataRow(SerializerLibraries.YamlDotNet, "Yaml", "pet:\n  kind: Dog\n  age: 3\n")]
        public async Task GenerateWithMemberInheritingOtherType_RoundTripFlatFormat(SerializerLibraries serializerLibraries, string format, string text)
        {
            var schema = CreateInheritedMemberSchema();
            var generator = TestHelper.CreateGenerator(schema, serializerLibraries, SchemaNamespace);
            var assembly = CompilerTestHelper.CompileToAssembly(generator.GenerateFile());
            var containerType = SerializerTestHelper.GetGeneratedType(assembly, "Container");
            var container = await SerializerTestHelper.Deserialize(assembly, $"DeserializeFrom{format}", containerType, text);
            var member = await WorkflowTestHelper.Select(container, "Pet");
            Assert.AreEqual("AnimalDog", member.GetType().Name, "Tagged member must deserialize as wrapper type.");
            Assert.AreEqual(3, await WorkflowTestHelper.Select(member, "Value.Age"), "Wrapper value must carry inherited properties.");

            var output = await SerializerTestHelper.Serialize(assembly, $"SerializeTo{format}", containerType, container);
            StringAssert.Contains(output, "Dog", "Serialized wrapper must carry its tag.");
            StringAssert.Contains(output, "3", "Serialized wrapper must carry inherited properties.");
            Assert.IsFalse(output.Contains("Value"), "Serialized wrapper must use the flat format.");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void GenerateWithMembersSharingBaseType_InheritBaseThroughUnion(bool unionInheritsBase)
        {
            var schema = CreateCommonBaseMemberSchema(unionInheritsBase);
            var generator = TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace);
            var code = generator.GenerateFile();
            Assert.IsTrue(code.Contains("class Animal : Mammal"), "Union base must inherit the shared base type.");
            Assert.IsTrue(code.Contains("class Cat : Animal"), "Member must inherit from union base type.");
            Assert.IsTrue(code.Contains("class Dog : Animal"), "Member must inherit from union base type.");
            Assert.IsFalse(code.Contains("IUnionWrapper, IUnionWrapper<"), "Members sharing a base type must not be wrapped.");
            CompilerTestHelper.CompileFromSource(code);
        }

        [TestMethod]
        [DataRow(SerializerLibraries.NewtonsoftJson, "Json", @"{""pet"":{""kind"":""Dog"",""age"":3}}")]
        [DataRow(SerializerLibraries.YamlDotNet, "Yaml", "pet:\n  kind: Dog\n  age: 3\n")]
        public async Task GenerateWithMembersSharingBaseType_RoundTripInheritedProperty(SerializerLibraries serializerLibraries, string format, string text)
        {
            var schema = CreateCommonBaseMemberSchema(unionInheritsBase: false);
            var generator = TestHelper.CreateGenerator(schema, serializerLibraries, SchemaNamespace);
            var assembly = CompilerTestHelper.CompileToAssembly(generator.GenerateFile());
            var containerType = SerializerTestHelper.GetGeneratedType(assembly, "Container");
            var container = await SerializerTestHelper.Deserialize(assembly, $"DeserializeFrom{format}", containerType, text);
            var member = await WorkflowTestHelper.Select(container, "Pet");
            Assert.AreEqual("Dog", member.GetType().Name, "Tagged member must deserialize as its own type.");
            Assert.AreEqual(3, await WorkflowTestHelper.Select(member, "Age"), "Member must carry inherited properties.");

            var output = await SerializerTestHelper.Serialize(assembly, $"SerializeTo{format}", containerType, container);
            StringAssert.Contains(output, "Dog", "Serialized member must carry its tag.");
            StringAssert.Contains(output, "3", "Serialized member must carry inherited properties.");
        }

        [TestMethod]
        public async Task GenerateWithMemberDiscriminatorProperty_ThrowsNamingProperty()
        {
            var schema = await CreateExternalMemberSchema();
            schema.Definitions["Hamster"].ActualProperties["kind"].ExtensionData!.Remove("const");
            var exception = Assert.ThrowsException<InvalidOperationException>(
                () => TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace).GenerateFile());
            StringAssert.Contains(exception.Message, "hamster");
            StringAssert.Contains(exception.Message, "kind");
        }

        [TestMethod]
        public async Task GenerateWithWrapperNameClash_ThrowsNamingWrapper()
        {
            var schema = await CreateExternalMemberSchema();
            schema.Definitions["AnimalHamster"] = new JsonSchema { Type = JsonObjectType.Object };
            var exception = Assert.ThrowsException<InvalidOperationException>(
                () => TestHelper.CreateGenerator(schema, schemaNamespace: SchemaNamespace).GenerateFile());
            StringAssert.Contains(exception.Message, "AnimalHamster");
        }
    }
}
