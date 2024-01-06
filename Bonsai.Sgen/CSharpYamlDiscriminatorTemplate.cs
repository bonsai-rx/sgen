using System.CodeDom;
using System.CodeDom.Compiler;

namespace Bonsai.Sgen
{
    internal class CSharpYamlDiscriminatorTemplate : CSharpCodeDomTemplate
    {
        public CSharpYamlDiscriminatorTemplate(
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(provider, options, settings)
        {
        }

        public override string TypeName => "YamlDiscriminatorAttribute";

        public override void BuildType(CodeTypeDeclaration type)
        {
            type.IsPartial = false;
            type.Attributes |= MemberAttributes.Family;
            type.BaseTypes.Add(typeof(Attribute));
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference(typeof(AttributeUsageAttribute)),
                new CodeAttributeArgument(new CodeBinaryOperatorExpression(
                    new CodeFieldReferenceExpression(
                        new CodeTypeReferenceExpression(typeof(AttributeTargets)),
                        nameof(AttributeTargets.Class)),
                    CodeBinaryOperatorType.BitwiseOr,
                    new CodeFieldReferenceExpression(
                        new CodeTypeReferenceExpression(typeof(AttributeTargets)),
                        nameof(AttributeTargets.Interface))))));
            type.Members.Add(new CodeSnippetTypeMember(
@"    public YamlDiscriminatorAttribute(string discriminator)
    {
        Discriminator = discriminator;
    }

    public string Discriminator { get; private set; }
"));
        }
    }
}
