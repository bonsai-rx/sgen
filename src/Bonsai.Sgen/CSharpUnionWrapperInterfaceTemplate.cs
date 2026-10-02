using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;

namespace Bonsai.Sgen
{
    internal class CSharpUnionWrapperInterfaceTemplate : CSharpCodeDomTemplate
    {
        public const string InterfaceName = "IUnionWrapper";

        public CSharpUnionWrapperInterfaceTemplate(
            bool isGeneric,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(provider, options, settings)
        {
            IsGeneric = isGeneric;
        }

        public bool IsGeneric { get; }

        public override string TypeName => InterfaceName;

        public override void BuildType(CodeTypeDeclaration type)
        {
            type.IsPartial = false;
            type.IsInterface = true;
            if (IsGeneric)
            {
                type.TypeParameters.Add(new CodeTypeParameter("out T"));
                type.Members.Add(new CodeSnippetTypeMember(
                    $"    T {JsonSchemaExtensions.UnionWrapperValueProperty} {{ get; }}"));
            }
            else
            {
                type.TypeAttributes = (type.TypeAttributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.NotPublic;
                type.Members.Add(new CodeSnippetTypeMember(
                    $"    object {JsonSchemaExtensions.UnionWrapperValueProperty} {{ get; set; }}"));
            }
        }
    }
}
