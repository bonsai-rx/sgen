using System.CodeDom;
using System.CodeDom.Compiler;
using Newtonsoft.Json;

namespace Bonsai.Sgen
{
    internal class CSharpUnionWrapperTemplate : CSharpCodeDomTemplate
    {
        public CSharpUnionWrapperTemplate(
            CSharpClassTemplateModel model,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(provider, options, settings)
        {
            Model = model;
        }

        public CSharpClassTemplateModel Model { get; }

        public override string TypeName => Model.ClassName;

        public override void BuildType(CodeTypeDeclaration type)
        {
            Model.Schema.TryGetUnionWrapperTag(out var tag);
            var discriminatorName = Model.Schema.InheritedSchema.ActualSchema.DiscriminatorObject.PropertyName;
            var valueSchema = Model.Schema.Properties[JsonSchemaExtensions.UnionWrapperValueProperty];
            var valueTypeName = Model.Resolver.Resolve(valueSchema, false, JsonSchemaExtensions.UnionWrapperValueProperty);
            var valueTypeOf = new CodeTypeOfExpression(valueTypeName);
            type.TypeAttributes = (type.TypeAttributes & ~System.Reflection.TypeAttributes.VisibilityMask) |
                System.Reflection.TypeAttributes.NotPublic |
                System.Reflection.TypeAttributes.Sealed;
            type.BaseTypes.Add(Model.BaseClassName);
            type.BaseTypes.Add(CSharpUnionWrapperInterfaceTemplate.InterfaceName);
            type.BaseTypes.Add(new CodeTypeReference(CSharpUnionWrapperInterfaceTemplate.InterfaceName, new CodeTypeReference(valueTypeName)));
            if (Settings.SerializerLibraries.HasFlag(SerializerLibraries.NewtonsoftJson))
            {
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(JsonConverter)),
                    new CodeAttributeArgument(new CodeTypeOfExpression(CSharpJsonUnionWrapperConverterTemplate.ConverterName)),
                    new CodeAttributeArgument(valueTypeOf),
                    new CodeAttributeArgument(new CodePrimitiveExpression(discriminatorName)),
                    new CodeAttributeArgument(new CodePrimitiveExpression(tag))));
            }
            if (Settings.SerializerLibraries.HasFlag(SerializerLibraries.YamlDotNet))
            {
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference(CSharpYamlUnionWrapperTemplate.AttributeName),
                    new CodeAttributeArgument(valueTypeOf)));
            }

            type.Members.Add(new CodeSnippetTypeMember(
@$"    private {valueTypeName} _value;

    private {TypeName}()
    {{
    }}

    internal {TypeName}({valueTypeName} value)
    {{
        _value = value;
    }}

    public {valueTypeName} {JsonSchemaExtensions.UnionWrapperValueProperty}
    {{
        get {{ return _value; }}
    }}

    object {CSharpUnionWrapperInterfaceTemplate.InterfaceName}.{JsonSchemaExtensions.UnionWrapperValueProperty}
    {{
        get {{ return _value; }}
        set {{ _value = ({valueTypeName})value; }}
    }}

    protected override bool PrintMembers(System.Text.StringBuilder stringBuilder)
    {{
        if (base.PrintMembers(stringBuilder))
        {{
            stringBuilder.Append("", "");
        }}
        stringBuilder.Append(""{JsonSchemaExtensions.UnionWrapperValueProperty} = "" + _value);
        return true;
    }}"));
        }
    }
}
