using System.CodeDom;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Bonsai.Sgen
{
    internal class CSharpTypeMatchTemplate : CSharpCodeDomTemplate
    {
        public CSharpTypeMatchTemplate(
            CSharpClassCodeArtifact modelType,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(provider, options, settings)
        {
            ModelType = modelType;
        }

        public CSharpClassCodeArtifact ModelType { get; }

        public override string TypeName => $"Match{ModelType.TypeName}";

        public override void BuildType(CodeTypeDeclaration type)
        {
            type.BaseTypes.Add(new CodeTypeReference("Bonsai.Expressions.SingleArgumentExpressionBuilder"));
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference(typeof(DefaultPropertyAttribute)),
                new CodeAttributeArgument(new CodePrimitiveExpression("Type"))));
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference("Bonsai.WorkflowElementCategoryAttribute"),
                new CodeAttributeArgument(new CodeFieldReferenceExpression(
                    new CodeTypeReferenceExpression("Bonsai.ElementCategory"),
                    "Combinator"))));
            foreach (var modelType in ModelType.Model.DerivedClasses)
            {
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(XmlIncludeAttribute)),
                    new CodeAttributeArgument(new CodeTypeOfExpression(
                        new CodeTypeReference(
                            "Bonsai.Expressions.TypeMapping",
                            new CodeTypeReference(modelType.ClassName))))));
            }

            type.Members.Add(new CodeSnippetTypeMember(
@$"    public Bonsai.Expressions.TypeMapping Type {{ get; set; }}

    public override System.Linq.Expressions.Expression Build(System.Collections.Generic.IEnumerable<System.Linq.Expressions.Expression> arguments)
    {{
        var typeMapping = Type;
        var returnType = typeMapping != null ? typeMapping.GetType().GetGenericArguments()[0] : typeof({ModelType.TypeName});
        return System.Linq.Expressions.Expression.Call(
            typeof({TypeName}),
            ""Process"",
            new System.Type[] {{ returnType }},
            System.Linq.Enumerable.Single(arguments));
    }}
"));
            var sourceTypeReference = new CodeTypeReference(ModelType.TypeName);
            var genericTypeParameter = new CodeTypeParameter("TResult") { Constraints = { sourceTypeReference } };
            var sourceParameter = new CodeParameterDeclarationExpression(
                new CodeTypeReference(typeof(IObservable<>)) { TypeArguments = { typeof(object) } }, "source");
            type.Members.Add(new CodeMemberMethod
            {
                Name = "Process",
                Attributes = MemberAttributes.Private | MemberAttributes.Static,
                TypeParameters = { genericTypeParameter },
                Parameters = { sourceParameter },
                ReturnType = new CodeTypeReference(typeof(IObservable<>))
                {
                    TypeArguments = { new CodeTypeReference(genericTypeParameter) }
                },
                Statements =
                {
                    new CodeExpressionStatement(new CodeSnippetExpression(
@$"return System.Reactive.Linq.Observable.Create<{genericTypeParameter.Name}>(observer =>
        {{
            var sourceObserver = System.Reactive.Observer.Create<object>(
                value =>
                {{
                    var match = value as {genericTypeParameter.Name};
                    if (match != null) observer.OnNext(match);
                }},
                observer.OnError,
                observer.OnCompleted);
            return System.ObservableExtensions.SubscribeSafe(source, sourceObserver);
        }})"))
                }
            });
        }
    }
}
