using System.CodeDom;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Xml.Serialization;
using NJsonSchema.CodeGeneration;
using NJsonSchema.CodeGeneration.CSharp.Models;

namespace Bonsai.Sgen
{
    internal class CSharpClassTemplate : ITemplate
    {
        public CSharpClassTemplate(ClassTemplateModel model, CodeDomProvider provider, CodeGeneratorOptions options)
        {
            Model = model;
            Provider = provider;
            Options = options;
        }

        public ClassTemplateModel Model { get; }

        public CodeDomProvider Provider { get; }

        public CodeGeneratorOptions Options { get; }

        public string Render()
        {
            var type = new CodeTypeDeclaration(Model.ClassName) { IsPartial = true };
            if (Model.HasDescription)
            {
                type.Comments.Add(new CodeCommentStatement("<summary>", docComment: true));
                type.Comments.Add(new CodeCommentStatement(Model.Description, docComment: true));
                type.Comments.Add(new CodeCommentStatement("</summary>", docComment: true));
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(DescriptionAttribute)),
                    new CodeAttributeArgument(new CodePrimitiveExpression(Model.Description))));
            }

            foreach (var property in Model.Properties)
            {
                var isPrimitive = PrimitiveTypes.TryGetValue(property.Type, out string? underlyingType);
                var fieldDeclaration = new CodeMemberField(
                    isPrimitive ? underlyingType : property.Type,
                    property.FieldName);
                if (property.HasDefaultValue)
                {
                    fieldDeclaration.InitExpression = new CodeSnippetExpression(property.DefaultValue);
                }

                var propertyDeclaration = new CodeMemberProperty
                {
                    Name = property.PropertyName,
                    Attributes = MemberAttributes.Public | MemberAttributes.Final,
                    Type = new CodeTypeReference(isPrimitive ? underlyingType : property.Type),
                    GetStatements =
                    {
                        new CodeMethodReturnStatement(new CodeSnippetExpression(property.FieldName))
                    },
                    SetStatements =
                    {
                        new CodeAssignStatement(
                            new CodeVariableReferenceExpression(property.FieldName),
                            new CodeVariableReferenceExpression("value"))
                    }
                };

                if (!isPrimitive || property.Type == "object")
                {
                    propertyDeclaration.CustomAttributes.Add(new CodeAttributeDeclaration(
                        new CodeTypeReference(typeof(XmlIgnoreAttribute))));
                }

                type.Members.Add(fieldDeclaration);
                type.Members.Add(propertyDeclaration);
            }

            using var writer = new StringWriter();
            Provider.GenerateCodeFromType(type, writer, Options);
            return writer.ToString();
        }

        static readonly Dictionary<string, string> PrimitiveTypes = new()
        {
            { "bool",    "System.Boolean" },
            { "byte",    "System.Byte" },
            { "sbyte",   "System.SByte" },
            { "char",    "System.Char" },
            { "decimal", "System.Decimal" },
            { "double",  "System.Double" },
            { "float",   "System.Single" },
            { "int",     "System.Int32" },
            { "uint",    "System.UInt32" },
            { "nint",    "System.IntPtr" },
            { "nuint",   "System.UIntPtr" },
            { "long",    "System.Int64" },
            { "ulong",   "System.UInt64" },
            { "short",   "System.Int16" },
            { "ushort",  "System.UInt16" },

            { "object",  "System.Object" },
            { "string",  "System.String" }
        };
    }
}
