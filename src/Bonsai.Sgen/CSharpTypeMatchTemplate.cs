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

            var matchType = unionWrappers.Count > 0
                ? $"        var matchType = typeof({ModelType.TypeName}).IsAssignableFrom(returnType) ? returnType : typeof({ModelType.TypeName});{Environment.NewLine}"
                : string.Empty;
            var matchTypeName = unionWrappers.Count > 0 ? "matchType" : "returnType";
            type.Members.Add(new CodeSnippetTypeMember(
@$"    public Bonsai.Expressions.TypeMapping Type {{ get; set; }}

    public override System.Linq.Expressions.Expression Build(System.Collections.Generic.IEnumerable<System.Linq.Expressions.Expression> arguments)
    {{
        var typeMapping = Type;
        var source = System.Linq.Enumerable.First(arguments);
        var inputType = source.Type.GetGenericArguments()[0];
        var elementType = inputType;
        var returnType = typeMapping != null ? typeMapping.GetType().GetGenericArguments()[0] : typeof({ModelType.TypeName});
        if (!elementType.IsInterface && !elementType.IsAssignableFrom(typeof({ModelType.TypeName})) && !typeof({ModelType.TypeName}).IsAssignableFrom(elementType))
        {{
            var value = System.Linq.Expressions.Expression.Parameter(elementType, ""value"");
            var conversion = System.Linq.Expressions.Expression.Convert(value, typeof({ModelType.TypeName}));
            source = System.Linq.Expressions.Expression.Call(
                typeof(System.Reactive.Linq.Observable),
                ""Select"",
                new System.Type[] {{ elementType, typeof({ModelType.TypeName}) }},
                source,
                System.Linq.Expressions.Expression.Lambda(conversion, value));
            elementType = typeof({ModelType.TypeName});
        }}

        if (returnType.IsAssignableFrom(elementType))
        {{
            return System.Linq.Expressions.Expression.Convert(
                source,
                typeof(System.IObservable<>).MakeGenericType(returnType));
        }}

{matchType}        if (!elementType.IsInterface && !elementType.IsAssignableFrom({matchTypeName}))
        {{
            throw new System.InvalidOperationException(
                ""The input type '"" + inputType + ""' can never match the type '"" + returnType + ""'."");
        }}

        return System.Linq.Expressions.Expression.Call(
            typeof({TypeName}),
            ""Process"",
            new System.Type[] {{ returnType }},
            source);
    }}
"));

            string match;
            var genericTypeParameter = new CodeTypeParameter("TResult");
            if (unionWrappers.Count > 0)
            {
                match =
@$"if (value is {genericTypeParameter.Name}) observer.OnNext(({genericTypeParameter.Name})value);
                    else
                    {{
                        var wrapper = value as {CSharpUnionWrapperInterfaceTemplate.InterfaceName}<{genericTypeParameter.Name}>;
                        if (wrapper != null) observer.OnNext(wrapper.{JsonSchemaExtensions.UnionWrapperValueProperty});
                    }}";
            }
            else
            {
                genericTypeParameter.Constraints.Add(new CodeTypeReference(ModelType.TypeName));
                match =
@$"var match = value as {genericTypeParameter.Name};
                    if (match != null) observer.OnNext(match);";
            }
            var sourceParameter = new CodeParameterDeclarationExpression(
                new CodeTypeReference(typeof(IObservable<>)) { TypeArguments = { new CodeTypeReference(typeof(object)) } }, "source");
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
                    {match}
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
