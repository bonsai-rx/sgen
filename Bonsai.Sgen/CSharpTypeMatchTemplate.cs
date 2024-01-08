using System.CodeDom;
using System.CodeDom.Compiler;
using NJsonSchema.CodeGeneration;

namespace Bonsai.Sgen
{
    internal class CSharpTypeMatchTemplate : CSharpCodeDomTemplate
    {
        public CSharpTypeMatchTemplate(
            CodeArtifact modelType,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(provider, options, settings)
        {
            ModelType = modelType;
        }

        public CodeArtifact ModelType { get; }

        public override string TypeName => $"{ModelType.TypeName}Match";

        public override void BuildType(CodeTypeDeclaration type)
        {
            var sourceParameter = new CodeParameterDeclarationExpression(
                new CodeTypeReference(typeof(IObservable<>))
                {
                    TypeArguments = { new CodeTypeReference(ModelType.BaseTypeName) }
                }, "source");
            var processMethod = new CodeMemberMethod
            {
                Name = "Process",
                Attributes = MemberAttributes.Public | MemberAttributes.Final,
                Parameters = { sourceParameter },
                ReturnType = new CodeTypeReference(typeof(IObservable<>))
                {
                    TypeArguments = { new CodeTypeReference(ModelType.TypeName) }
                },
                Statements =
                {
                    new CodeExpressionStatement(new CodeSnippetExpression(
@$"return System.Reactive.Linq.Observable.Create<{ModelType.TypeName}>(observer =>
        {{
            var sourceObserver = System.Reactive.Observer.Create<{ModelType.BaseTypeName}>(
                value =>
                {{
                    var match = value as {ModelType.TypeName};
                    if (match != null) observer.OnNext(match);
                }},
                observer.OnError,
                observer.OnCompleted);
            return System.ObservableExtensions.SubscribeSafe(source, sourceObserver);
        }});"))
                }
            };
            type.Members.Add(processMethod);
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference("Bonsai.CombinatorAttribute")));
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference("Bonsai.WorkflowElementCategoryAttribute"),
                new CodeAttributeArgument(new CodeFieldReferenceExpression(
                    new CodeTypeReferenceExpression("Bonsai.ElementCategory"),
                    "Combinator"))));
        }
    }
}
