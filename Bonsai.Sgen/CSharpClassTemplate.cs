using System.CodeDom;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Text;
using System.Xml.Serialization;
using Newtonsoft.Json;
using NJsonSchema.CodeGeneration;
using NJsonSchema.Converters;
using YamlDotNet.Serialization;

namespace Bonsai.Sgen
{
    internal class CSharpClassTemplate : ITemplate
    {
        public CSharpClassTemplate(
            CSharpClassTemplateModel model,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
        {
            Model = model;
            Provider = provider;
            Options = options;
            Settings = settings;
        }

        public CSharpClassTemplateModel Model { get; }

        public CodeDomProvider Provider { get; }

        public CodeGeneratorOptions Options { get; }

        public CSharpCodeDomGeneratorSettings Settings { get; }

        public string Render()
        {
            var type = new CodeTypeDeclaration(Model.ClassName) { IsPartial = true };
            if (Model.IsAbstract) type.TypeAttributes |= System.Reflection.TypeAttributes.Abstract;
            if (Model.HasDiscriminator)
            {
                if (Settings.SerializerLibraries.HasFlag(SerializerLibraries.NewtonsoftJson))
                {
                    type.CustomAttributes.Add(new CodeAttributeDeclaration(
                        new CodeTypeReference(typeof(JsonConverter)),
                        new CodeAttributeArgument(new CodeTypeOfExpression(nameof(JsonInheritanceConverter))),
                        new CodeAttributeArgument(new CodePrimitiveExpression(Model.Discriminator))));
                    foreach (var derivedModel in Model.DerivedClasses)
                    {
                        type.CustomAttributes.Add(new CodeAttributeDeclaration(
                            new CodeTypeReference(nameof(JsonInheritanceAttribute)),
                            new CodeAttributeArgument(new CodePrimitiveExpression(derivedModel.Discriminator)),
                            new CodeAttributeArgument(new CodeTypeOfExpression(derivedModel.ClassName))));
                    }
                }
            }

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
                Model.Schema.ActualProperties.TryGetValue(property.Name, out var propertySchema);
                var isPrimitive = PrimitiveTypes.TryGetValue(property.Type, out string? underlyingType);
                var fieldDeclaration = new CodeMemberField(
                    isPrimitive ? underlyingType : property.Type,
                    property.FieldName);
                if (property.HasDefaultValue)
                {
                    fieldDeclaration.InitExpression = new CodeSnippetExpression(property.DefaultValue);
                }
                else if (propertySchema?.IsArray is true)
                {
                    fieldDeclaration.InitExpression = new CodeObjectCreateExpression(fieldDeclaration.Type);
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

                if (Settings.SerializerLibraries.HasFlag(SerializerLibraries.NewtonsoftJson))
                {
                    var jsonProperty = new CodeAttributeDeclaration(
                        new CodeTypeReference(typeof(JsonPropertyAttribute)),
                        new CodeAttributeArgument(new CodePrimitiveExpression(property.Name)));
                    if (property.IsRequired && Settings.RequiredPropertiesMustBeDefined)
                    {
                        jsonProperty.Arguments.Add(new CodeAttributeArgument(
                            nameof(JsonPropertyAttribute.Required),
                            new CodeFieldReferenceExpression(
                                new CodeTypeReferenceExpression(typeof(Required)),
                                nameof(Required.Always))));
                    }
                    propertyDeclaration.CustomAttributes.Add(jsonProperty);
                }
                if (Settings.SerializerLibraries.HasFlag(SerializerLibraries.YamlDotNet))
                {
                    propertyDeclaration.CustomAttributes.Add(new CodeAttributeDeclaration(
                        new CodeTypeReference(typeof(YamlMemberAttribute)),
                        new CodeAttributeArgument(
                            nameof(YamlMemberAttribute.Alias),
                            new CodePrimitiveExpression(property.Name))));
                }

                if (property.HasDescription)
                {
                    propertyDeclaration.Comments.Add(new CodeCommentStatement("<summary>", docComment: true));
                    propertyDeclaration.Comments.Add(new CodeCommentStatement(property.Description, docComment: true));
                    propertyDeclaration.Comments.Add(new CodeCommentStatement("</summary>", docComment: true));
                    propertyDeclaration.CustomAttributes.Add(new CodeAttributeDeclaration(
                        new CodeTypeReference(typeof(DescriptionAttribute)),
                        new CodeAttributeArgument(new CodePrimitiveExpression(property.Description))));
                }

                type.Members.Add(fieldDeclaration);
                type.Members.Add(propertyDeclaration);
            }

            var defaultConstructor = new CodeConstructor { Attributes = Model.IsAbstract ? MemberAttributes.Family : MemberAttributes.Public };
            var copyConstructor = new CodeConstructor { Attributes = MemberAttributes.Family };
            var copyParameter = new CodeParameterDeclarationExpression(Model.ClassName, "other");
            copyConstructor.Parameters.Add(copyParameter);
            if (Model.BaseClass != null)
            {
                type.BaseTypes.Add(Model.BaseClassName);
                copyConstructor.BaseConstructorArgs.Add(new CodeVariableReferenceExpression(copyParameter.Name));
            }
            foreach (var property in Model.Properties)
            {
                copyConstructor.Statements.Add(new CodeAssignStatement(
                    new CodeVariableReferenceExpression(property.FieldName),
                    new CodeFieldReferenceExpression(new CodeVariableReferenceExpression(copyParameter.Name), property.FieldName)));
            }
            type.Members.Add(defaultConstructor);
            type.Members.Add(copyConstructor);

            if (Model.GenerateJsonMethods && !Model.IsAbstract)
            {
                var processMethod = new CodeMemberMethod
                {
                    Name = "Process",
                    Attributes = MemberAttributes.Public | MemberAttributes.Final,
                    ReturnType = new CodeTypeReference(typeof(IObservable<>))
                    {
                        TypeArguments = { new CodeTypeReference(Model.ClassName) }
                    }
                };
                processMethod.Statements.Add(new CodeMethodReturnStatement(
                    new CodeMethodInvokeExpression(
                        new CodeMethodReferenceExpression(
                            new CodeTypeReferenceExpression("System.Reactive.Linq.Observable"),
                            "Defer"),
                        new CodeSnippetExpression(
                            @$"() => System.Reactive.Linq.Observable.Return(new {Model.ClassName}(this))"))));
                type.Members.Add(processMethod);
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference("Bonsai.CombinatorAttribute")));
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference("Bonsai.WorkflowElementCategoryAttribute"),
                    new CodeAttributeArgument(new CodeFieldReferenceExpression(
                        new CodeTypeReferenceExpression("Bonsai.ElementCategory"),
                        "Source"))));
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
