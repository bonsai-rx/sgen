using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;

namespace Bonsai.Sgen
{
    internal class CSharpYamlUnionWrapperTemplate : CSharpCodeDomTemplate
    {
        public const string AttributeName = "YamlUnionWrapper";

        public CSharpYamlUnionWrapperTemplate(
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(provider, options, settings)
        {
        }

        public override string TypeName => $"{AttributeName}Attribute";

        public override void BuildType(CodeTypeDeclaration type)
        {
            type.IsPartial = false;
            type.TypeAttributes = (type.TypeAttributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.NotPublic;
            type.BaseTypes.Add(typeof(Attribute));
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference(typeof(AttributeUsageAttribute)),
                new CodeAttributeArgument(new CodeFieldReferenceExpression(
                    new CodeTypeReferenceExpression(typeof(AttributeTargets)),
                    nameof(AttributeTargets.Class)))));
            type.Members.Add(new CodeSnippetTypeMember(
@$"    public {TypeName}(System.Type valueType)
    {{
        ValueType = valueType;
    }}

    public System.Type ValueType {{ get; private set; }}
"));
        }
    }
}
