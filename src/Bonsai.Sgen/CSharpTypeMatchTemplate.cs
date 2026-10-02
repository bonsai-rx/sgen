using System.CodeDom;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Bonsai.Sgen
{
    internal class CSharpTypeMatchTemplate : CSharpCodeDomTemplate
    {
        const string UnwrapMethodName = "ProcessUnwrap";

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
            var unionWrappers = ModelType.Model.GetUnionWrappers().ToDictionary(wrapper => wrapper.TypeName);
            foreach (var modelType in ModelType.Model.DerivedClasses)
            {
                var typeMappingArgument = unionWrappers.TryGetValue(modelType.ClassName, out var wrapper)
                    ? wrapper.ValueTypeName
                    : modelType.ClassName;
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(XmlIncludeAttribute)),
                    new CodeAttributeArgument(new CodeTypeOfExpression(
                        new CodeTypeReference(
                            "Bonsai.Expressions.TypeMapping",
                            new CodeTypeReference(typeMappingArgument))))));
            }

            var wrapperMatch = unionWrappers.Count > 0
                ?
@$"        if (!typeof({ModelType.TypeName}).IsAssignableFrom(returnType))
        {{
            return System.Linq.Expressions.Expression.Call(
                typeof({TypeName}),
                ""{UnwrapMethodName}"",
                new System.Type[] {{ returnType }},
                source);
        }}
"
                : string.Empty;
            type.Members.Add(new CodeSnippetTypeMember(
@$"    public Bonsai.Expressions.TypeMapping Type {{ get; set; }}

    public override System.Linq.Expressions.Expression Build(System.Collections.Generic.IEnumerable<System.Linq.Expressions.Expression> arguments)
    {{
        var typeMapping = Type;
        var source = System.Linq.Enumerable.First(arguments);
        var returnType = typeMapping != null ? typeMapping.GetType().GetGenericArguments()[0] : typeof({ModelType.TypeName});
        if (returnType == typeof({ModelType.TypeName}) && !typeof(System.IObservable<{ModelType.TypeName}>).IsAssignableFrom(source.Type))
        {{
            var elementType = source.Type.GetGenericArguments()[0];
            var value = System.Linq.Expressions.Expression.Parameter(elementType, ""value"");
            var conversion = System.Linq.Expressions.Expression.Lambda(
                System.Linq.Expressions.Expression.Convert(value, returnType),
                value);
            return System.Linq.Expressions.Expression.Call(
                typeof(System.Reactive.Linq.Observable),
                ""Select"",
                new System.Type[] {{ elementType, returnType }},
                source,
                conversion);
        }}
{wrapperMatch}        return System.Linq.Expressions.Expression.Call(
            typeof({TypeName}),
            ""Process"",
            new System.Type[] {{ returnType }},
            source);
    }}
"));

            if (unionWrappers.Count > 0)
            {
                type.Members.Add(new CodeSnippetTypeMember(
@$"    private static System.IObservable<TValue> {UnwrapMethodName}<TValue>(System.IObservable<{ModelType.TypeName}> source)
    {{
        return System.Reactive.Linq.Observable.Create<TValue>(observer =>
        {{
            var sourceObserver = System.Reactive.Observer.Create<{ModelType.TypeName}>(
                value =>
                {{
                    var match = value as {CSharpUnionWrapperInterfaceTemplate.InterfaceName}<TValue>;
                    if (match != null) observer.OnNext(match.{JsonSchemaExtensions.UnionWrapperValueProperty});
                }},
                observer.OnError,
                observer.OnCompleted);
            return System.ObservableExtensions.SubscribeSafe(source, sourceObserver);
        }});
    }}
"));
            }
            var sourceTypeReference = new CodeTypeReference(ModelType.TypeName);
            var genericTypeParameter = new CodeTypeParameter("TResult") { Constraints = { sourceTypeReference } };
            var sourceParameter = new CodeParameterDeclarationExpression(
                new CodeTypeReference(typeof(IObservable<>)) { TypeArguments = { sourceTypeReference } }, "source");
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
            var sourceObserver = System.Reactive.Observer.Create<{ModelType.TypeName}>(
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
