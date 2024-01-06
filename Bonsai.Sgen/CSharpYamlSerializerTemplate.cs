using System.CodeDom;
using System.CodeDom.Compiler;
using NJsonSchema;
using NJsonSchema.CodeGeneration;

namespace Bonsai.Sgen
{
    internal class CSharpYamlSerializerTemplate : CSharpSerializerTemplate
    {
        public CSharpYamlSerializerTemplate(
            IEnumerable<CodeArtifact> modelTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(modelTypes, provider, options, settings)
        {
        }

        public override string ClassName => "SerializeToYaml";

        public override string Description => "Serializes a sequence of data model objects into YAML strings.";

        public override void RenderType(CodeTypeDeclaration type)
        {
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference("Bonsai.CombinatorAttribute")));
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference("Bonsai.WorkflowElementCategoryAttribute"),
                new CodeAttributeArgument(new CodeFieldReferenceExpression(
                    new CodeTypeReferenceExpression("Bonsai.ElementCategory"),
                    "Transform"))));
            type.Members.Add(new CodeSnippetTypeMember(
@"    private System.IObservable<string> Process<T>(System.IObservable<T> source)
    {
        return System.Reactive.Linq.Observable.Defer(() =>
        {
            var serializer = new YamlDotNet.Serialization.SerializerBuilder().Build();
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
            IEnumerable<CodeArtifact> modelTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(schema, modelTypes, provider, options, settings)
        {
        }

        public override string ClassName => "DeserializeFromYaml";

        public override string Description => "Deserializes a sequence of YAML strings into data model objects.";

        public override void RenderType(CodeTypeDeclaration type)
        {
            base.RenderType(type);
            type.Members.Add(new CodeSnippetTypeMember(
@"    private static System.IObservable<T> Process<T>(System.IObservable<string> source)
    {
        return System.Reactive.Linq.Observable.Defer(() =>
        {
            var serializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
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
