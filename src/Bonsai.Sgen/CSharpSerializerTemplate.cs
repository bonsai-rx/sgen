using System.CodeDom;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Xml.Serialization;
using NJsonSchema;
using NJsonSchema.CodeGeneration;

namespace Bonsai.Sgen
{
    internal abstract class CSharpSerializerTemplate : CSharpCodeDomTemplate
    {
        public CSharpSerializerTemplate(
            IEnumerable<CSharpClassCodeArtifact> modelTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(provider, options, settings)
        {
            ModelTypes = modelTypes;
        }

        public IEnumerable<CSharpClassCodeArtifact> ModelTypes { get; }

        public abstract string Description { get; }

        public override void BuildType(CodeTypeDeclaration type)
        {
            var description = Description;
            if (!string.IsNullOrEmpty(description))
            {
                type.Comments.Add(new CodeCommentStatement("<summary>", docComment: true));
                type.Comments.Add(new CodeCommentStatement(description, docComment: true));
                type.Comments.Add(new CodeCommentStatement("</summary>", docComment: true));
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference(typeof(DescriptionAttribute)),
                    new CodeAttributeArgument(new CodePrimitiveExpression(description))));
            }
        }
    }

    internal abstract class CSharpDeserializerTemplate : CSharpSerializerTemplate
    {
        readonly JsonSchema _schema;

        public CSharpDeserializerTemplate(
            JsonSchema schema,
            IEnumerable<CSharpClassCodeArtifact> modelTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(modelTypes, provider, options, settings)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        public override void BuildType(CodeTypeDeclaration type)
        {
            base.BuildType(type);
            type.BaseTypes.Add(new CodeTypeReference("Bonsai.Expressions.SingleArgumentExpressionBuilder"));
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference(typeof(DefaultPropertyAttribute)),
                new CodeAttributeArgument(new CodePrimitiveExpression("Type"))));
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference("Bonsai.WorkflowElementCategoryAttribute"),
                new CodeAttributeArgument(new CodeFieldReferenceExpression(
                    new CodeTypeReferenceExpression("Bonsai.ElementCategory"),
                    "Transform"))));
            foreach (var modelType in ModelTypes)
            {
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(XmlIncludeAttribute)),
                    new CodeAttributeArgument(new CodeTypeOfExpression(
                        new CodeTypeReference(
                            "Bonsai.Expressions.TypeMapping",
                            new CodeTypeReference(modelType.TypeName))))));
            }

            var defaultType = ModelTypes.FirstOrDefault(
                modelType => modelType.Model.Schema == _schema,
                ModelTypes.First());

            type.Members.Add(new CodeSnippetTypeMember(
@$"    public {TypeName}()
    {{
        Type = new Bonsai.Expressions.TypeMapping<{defaultType.TypeName}>();
    }}

    public Bonsai.Expressions.TypeMapping Type {{ get; set; }}

    public override System.Linq.Expressions.Expression Build(System.Collections.Generic.IEnumerable<System.Linq.Expressions.Expression> arguments)
    {{
        var typeMapping = (Bonsai.Expressions.TypeMapping)Type;
        var returnType = typeMapping.GetType().GetGenericArguments()[0];
        return System.Linq.Expressions.Expression.Call(
            typeof({TypeName}),
            ""Process"",
            new System.Type[] {{ returnType }},
            System.Linq.Enumerable.Single(arguments));
    }}
"));
        }
    }
}
