using System.CodeDom;
using System.CodeDom.Compiler;
using NJsonSchema;
using NJsonSchema.CodeGeneration;

namespace Bonsai.Sgen
{
    internal static class CSharpYamlDiscriminatorTemplateHelper
    {
        public static string RenderDiscriminatorTypeInspector(IEnumerable<CodeArtifact> discriminatorTypes, bool ignoreInheritedDiscriminators)
        {
            return discriminatorTypes.Any()
                ?
@$"                .WithTypeInspector(inspector => new YamlDiscriminatorTypeInspector(inspector{(ignoreInheritedDiscriminators ? ", true" : string.Empty)}))
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

    class UnionNodeDeserializer : YamlDotNet.Serialization.INodeDeserializer
    {
        readonly YamlDotNet.Serialization.INodeDeserializer innerDeserializer;

        public UnionNodeDeserializer(YamlDotNet.Serialization.INodeDeserializer innerDeserializer)
        {
            this.innerDeserializer = innerDeserializer;
        }

        public bool Deserialize(
            YamlDotNet.Core.IParser reader,
            System.Type expectedType,
            System.Func<YamlDotNet.Core.IParser, System.Type, object> nestedObjectDeserializer,
            out object value,
            YamlDotNet.Serialization.ObjectDeserializer rootDeserializer)
        {
            if (!System.Attribute.IsDefined(expectedType, typeof(YamlDiscriminatorAttribute), false))
            {
                value = null;
                return false;
            }

            return innerDeserializer.Deserialize(reader, expectedType, nestedObjectDeserializer, out value, rootDeserializer);
        }
    }
"));
                return @$"                .WithTypeDiscriminatingNodeDeserializer(o =>
                {{
{string.Join(Environment.NewLine, discriminatorTypes.Select(type =>
                $"                    AddTypeDiscriminator<{type.TypeName}>(o);"))}
                }})
                .WithNodeDeserializer(
                    inner => new UnionNodeDeserializer(inner),
                    s => s.InsteadOf<YamlDotNet.Serialization.BufferedDeserialization.TypeDiscriminatingNodeDeserializer>())
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
            var typeInspector = CSharpYamlDiscriminatorTemplateHelper.RenderDiscriminatorTypeInspector(DiscriminatorTypes, false);
            type.Members.Add(new CodeSnippetTypeMember(
@"    private System.IObservable<string> Process<T>(System.IObservable<T> source)
    {
        return System.Reactive.Linq.Observable.Defer(() =>
        {
            var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                .WithQuotingNecessaryStrings()
                .WithYamlFormatter(new YamlDotNet.Serialization.YamlFormatter { PotentiallyQuoteEnums = _ => false })
" + typeInspector +
@"                .WithTypeConverter(new YamlDotNet.Serialization.Converters.DateTimeOffsetConverter())
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
            bool hasUnionWrappers,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(schema, modelTypes, provider, options, settings)
        {
            DiscriminatorTypes = discriminatorTypes;
            HasUnionWrappers = hasUnionWrappers;
        }

        public override string TypeName => "DeserializeFromYaml";

        public override string Description => "Deserializes a sequence of YAML strings into data model objects.";

        public IEnumerable<CodeArtifact> DiscriminatorTypes { get; }

        public bool HasUnionWrappers { get; }

        public override void BuildType(CodeTypeDeclaration type)
        {
            base.BuildType(type);
            var typeInspector = CSharpYamlDiscriminatorTemplateHelper.RenderDiscriminatorTypeInspector(DiscriminatorTypes, true);
            var typeDiscriminators = CSharpYamlDiscriminatorTemplateHelper.RenderTypeDiscriminators(type, DiscriminatorTypes);
            var privateConstructors = HasUnionWrappers
                ?
@"                .EnablePrivateConstructors()
"
                : string.Empty;
            type.Members.Add(new CodeSnippetTypeMember(
@"    private static System.IObservable<T> Process<T>(System.IObservable<string> source)
    {
        return System.Reactive.Linq.Observable.Defer(() =>
        {
            var serializer = new YamlDotNet.Serialization.DeserializerBuilder()
" + typeInspector + typeDiscriminators + privateConstructors +
@"                .WithTypeConverter(new YamlDotNet.Serialization.Converters.DateTimeOffsetConverter())
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
