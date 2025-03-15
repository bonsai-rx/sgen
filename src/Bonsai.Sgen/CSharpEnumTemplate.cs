using System.CodeDom;
using System.CodeDom.Compiler;
using NJsonSchema.CodeGeneration.CSharp.Models;
using YamlDotNet.Serialization;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Bonsai.Sgen
{
    internal class CSharpEnumTemplate : CSharpCodeDomTemplate
    {
        public CSharpEnumTemplate(
            EnumTemplateModel model,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(provider, options, settings)
        {
            Model = model;
        }

        public EnumTemplateModel Model { get; }

        public override string TypeName => Model.Name;

        public override void BuildType(CodeTypeDeclaration type)
        {
            type.IsPartial = false;
            type.IsEnum = true;
            if (Model.HasDescription)
            {
                type.Comments.Add(new CodeCommentStatement("<summary>", docComment: true));
                type.Comments.Add(new CodeCommentStatement(Model.Description, docComment: true));
                type.Comments.Add(new CodeCommentStatement("</summary>", docComment: true));
            }

            if (Model.IsStringEnum && Settings.SerializerLibraries.HasFlag(SerializerLibraries.NewtonsoftJson))
            {
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(JsonConverter)),
                    new CodeAttributeArgument(new CodeTypeOfExpression(typeof(StringEnumConverter)))));
            }

            if (Model.IsEnumAsBitFlags)
            {
                type.CustomAttributes.Add(new CodeAttributeDeclaration(typeof(FlagsAttribute).FullName));
            }

            foreach (var enumValue in Model.Enums)
            {
                var valueDeclaration = new CodeMemberField(type.Name, enumValue.Name);
                if (Settings.SerializerLibraries.HasFlag(SerializerLibraries.NewtonsoftJson))
                {
                    valueDeclaration.CustomAttributes.Add(new CodeAttributeDeclaration(
                        new CodeTypeReference(typeof(EnumMemberAttribute)),
                        new CodeAttributeArgument(
                            nameof(EnumMemberAttribute.Value),
                            new CodePrimitiveExpression(enumValue.Value))));
                }
                if (Settings.SerializerLibraries.HasFlag(SerializerLibraries.YamlDotNet))
                {
                    valueDeclaration.CustomAttributes.Add(new CodeAttributeDeclaration(
                        new CodeTypeReference(typeof(YamlMemberAttribute)),
                        new CodeAttributeArgument(
                            nameof(YamlMemberAttribute.Alias),
                            new CodePrimitiveExpression(enumValue.Value))));
                }

                valueDeclaration.InitExpression = new CodeSnippetExpression(enumValue.InternalValue);
                type.Members.Add(valueDeclaration);
            }
        }
    }
}
