using System.CodeDom;
using System.CodeDom.Compiler;
using NJsonSchema.CodeGeneration;
using NJsonSchema.CodeGeneration.CSharp.Models;
using YamlDotNet.Serialization;
using System.Runtime.Serialization;

namespace Bonsai.Sgen
{
    internal class CSharpEnumTemplate : ITemplate
    {
        public CSharpEnumTemplate(
            EnumTemplateModel model,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
        {
            Model = model;
            Provider = provider;
            Options = options;
            Settings = settings;
        }

        public EnumTemplateModel Model { get; }

        public CodeDomProvider Provider { get; }

        public CodeGeneratorOptions Options { get; }

        public CSharpCodeDomGeneratorSettings Settings { get; }

        public string Render()
        {
            var type = new CodeTypeDeclaration(Model.Name) { IsEnum = true };
            if (Model.HasDescription)
            {
                type.Comments.Add(new CodeCommentStatement("<summary>", docComment: true));
                type.Comments.Add(new CodeCommentStatement(Model.Description, docComment: true));
                type.Comments.Add(new CodeCommentStatement("</summary>", docComment: true));
            }

            if (Model.IsEnumAsBitFlags)
            {
                type.CustomAttributes.Add(new CodeAttributeDeclaration(typeof(FlagsAttribute).FullName));
            }

            foreach (var enumValue in Model.Enums)
            {
                var valueDeclaration = new CodeMemberField(type.Name, enumValue.Name);
                if (Settings.SerializerLibraries.HasFlag(SerializerLibraries.NewtonsoftJson) ||
                    Settings.SerializerLibraries.HasFlag(SerializerLibraries.SystemTextJson))
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

            using var writer = new StringWriter();
            Provider.GenerateCodeFromType(type, writer, Options);
            return writer.ToString();
        }
    }
}
