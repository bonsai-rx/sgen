using System.CodeDom;
using System.CodeDom.Compiler;
using NJsonSchema.CodeGeneration;
using NJsonSchema.CodeGeneration.CSharp.Models;

namespace Bonsai.Sgen
{
    internal class CSharpEnumTemplate : ITemplate
    {
        public CSharpEnumTemplate(EnumTemplateModel model, CodeDomProvider provider, CodeGeneratorOptions options)
        {
            Model = model;
            Provider = provider;
            Options = options;
        }

        public EnumTemplateModel Model { get; }

        public CodeDomProvider Provider { get; }

        public CodeGeneratorOptions Options { get; }

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
                valueDeclaration.InitExpression = new CodeSnippetExpression(enumValue.InternalValue);
                type.Members.Add(valueDeclaration);
            }

            using var writer = new StringWriter();
            Provider.GenerateCodeFromType(type, writer, Options);
            return writer.ToString();
        }
    }
}
