using System.CodeDom;
using System.CodeDom.Compiler;
using NJsonSchema;
using NJsonSchema.CodeGeneration;

namespace Bonsai.Sgen
{
    internal class CSharpJsonSerializerTemplate : CSharpSerializerTemplate
    {
        public CSharpJsonSerializerTemplate(
            IEnumerable<CodeArtifact> modelTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(modelTypes, provider, options, settings)
        {
        }

        public override string ClassName => "SerializeToJson";

        public override string Description => "Serializes a sequence of data model objects into JSON strings.";

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
        return System.Reactive.Linq.Observable.Select(source, value => Newtonsoft.Json.JsonConvert.SerializeObject(value));
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

    internal class CSharpJsonDeserializerTemplate : CSharpDeserializerTemplate
    {
        public CSharpJsonDeserializerTemplate(
            JsonSchema schema,
            IEnumerable<CodeArtifact> modelTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(schema, modelTypes, provider, options, settings)
        {
        }

        public override string ClassName => "DeserializeFromJson";

        public override string Description => "Deserializes a sequence of JSON strings into data model objects.";

        public override void RenderType(CodeTypeDeclaration type)
        {
            base.RenderType(type);
            type.Members.Add(new CodeSnippetTypeMember(
@"    private static System.IObservable<T> Process<T>(System.IObservable<string> source)
    {
        return System.Reactive.Linq.Observable.Select(source, value => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(value));
    }"));
        }
    }
}
