using System.CodeDom;
using System.CodeDom.Compiler;
using NJsonSchema;
using NJsonSchema.CodeGeneration;

namespace Bonsai.Sgen
{
    internal static class CSharpYamlDiscriminatorTemplateHelper
    {
        public static string RenderDiscriminatorTypeInspector(IEnumerable<CodeArtifact> discriminatorTypes)
        {
            return discriminatorTypes.Any()
                ?
@$"                .WithTypeInspector(inspector => new YamlDiscriminatorTypeInspector(inspector))
"
                : string.Empty;
        }

        public static string RenderTypeDiscriminators(CodeTypeDeclaration type, IEnumerable<CodeArtifact> discriminatorTypes)
        {
            if (discriminatorTypes.Any())
            {
                type.Members.Add(new CodeSnippetTypeMember(
@"    private static void AddTypeDiscriminator<T>(YamlDotNet.Serialization.BufferedDeserialization.ITypeDiscriminatingNodeDeserializerOptions o)
    {
        var baseType = typeof(T);
        var discriminator = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<YamlDiscriminatorAttribute>(baseType).Discriminator;
        var typeMapping = System.Linq.Enumerable.ToDictionary(
            System.Reflection.CustomAttributeExtensions.GetCustomAttributes<JsonInheritanceAttribute>(baseType),
            attr => attr.Key,
            attr => attr.Type);
        o.AddKeyValueTypeDiscriminator<T>(discriminator, typeMapping);
    }
"));
                return @$"                .WithTypeDiscriminatingNodeDeserializer(o =>
                {{
{string.Join("\r\n", discriminatorTypes.Select(type =>
                $"                    AddTypeDiscriminator<{type.TypeName}>(o);"))}
                }})
";
            }

            return string.Empty;
        }
    }

    internal class CSharpYamlSerializerTemplate : CSharpSerializerTemplate
    {
        public CSharpYamlSerializerTemplate(
            IEnumerable<CSharpClassCodeArtifact> modelTypes,
            IEnumerable<CSharpClassCodeArtifact> discriminatorTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(modelTypes, provider, options, settings)
        {
            DiscriminatorTypes = discriminatorTypes;
        }

        public override string TypeName => "SerializeToYaml";

        public override string Description => "Serializes a sequence of data model objects into YAML strings.";

        public IEnumerable<CodeArtifact> DiscriminatorTypes { get; }

        public override void BuildType(CodeTypeDeclaration type)
        {
            base.BuildType(type);
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference("Bonsai.WorkflowElementCategoryAttribute"),
                new CodeAttributeArgument(new CodeFieldReferenceExpression(
                    new CodeTypeReferenceExpression("Bonsai.ElementCategory"),
                    "Transform"))));
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference("Bonsai.CombinatorAttribute")));
            var typeInspector = CSharpYamlDiscriminatorTemplateHelper.RenderDiscriminatorTypeInspector(DiscriminatorTypes);
            type.Members.Add(new CodeSnippetTypeMember(
@"    private System.IObservable<string> Process<T>(System.IObservable<T> source)
    {
        return System.Reactive.Linq.Observable.Defer(() =>
        {
            var serializer = new YamlDotNet.Serialization.SerializerBuilder()
" + typeInspector +
@"                  .WithTypeConverter(new YamlDotNet.Serialization.Converters.DateTimeOffsetConverter())
                  .Build();
            return System.Reactive.Linq.Observable.Select(source, value => serializer.Serialize(value)); 
        });
    }"));
            foreach (var modelType in ModelTypes)
            {
                type.Members.Add(new CodeSnippetTypeMember(@$"
    public System.IObservable<string> Process(System.IObservable<{modelType.TypeName}> source)
    {{
        return Process<{modelType.TypeName}>(source);
    }}"));
            }
        }
    }

    internal class CSharpYamlDeserializerTemplate : CSharpDeserializerTemplate
    {
        public CSharpYamlDeserializerTemplate(
            JsonSchema schema,
            IEnumerable<CSharpClassCodeArtifact> modelTypes,
            IEnumerable<CSharpClassCodeArtifact> discriminatorTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(schema, modelTypes, provider, options, settings)
        {
            DiscriminatorTypes = discriminatorTypes;
        }

        public override string TypeName => "DeserializeFromYaml";

        public override string Description => "Deserializes a sequence of YAML strings into data model objects.";

        public IEnumerable<CodeArtifact> DiscriminatorTypes { get; }

        public override void BuildType(CodeTypeDeclaration type)
        {
            base.BuildType(type);
            var typeInspector = CSharpYamlDiscriminatorTemplateHelper.RenderDiscriminatorTypeInspector(DiscriminatorTypes);
            var typeDiscriminators = CSharpYamlDiscriminatorTemplateHelper.RenderTypeDiscriminators(type, DiscriminatorTypes);
            type.Members.Add(new CodeSnippetTypeMember(
@"    private static System.IObservable<T> Process<T>(System.IObservable<string> source)
    {
        return System.Reactive.Linq.Observable.Defer(() =>
        {
            var serializer = new YamlDotNet.Serialization.DeserializerBuilder()
" + typeInspector + typeDiscriminators +
@"                  .WithTypeConverter(new YamlDotNet.Serialization.Converters.DateTimeOffsetConverter())
                  .Build();
            return System.Reactive.Linq.Observable.Select(source, value =>
            {
                var reader = new System.IO.StringReader(value);
                var parser = new YamlDotNet.Core.MergingParser(new YamlDotNet.Core.Parser(reader));
                return serializer.Deserialize<T>(parser);
            });
        });
    }"));
        }
    }
}
