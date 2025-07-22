using System.CodeDom;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Text;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp.Models;
using NJsonSchema.Converters;
using YamlDotNet.Serialization;

namespace Bonsai.Sgen
{
    internal class CSharpClassTemplate : CSharpCodeDomTemplate
    {
        public CSharpClassTemplate(
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
            var jsonSerializer = Settings.SerializerLibraries.HasFlag(SerializerLibraries.NewtonsoftJson);
            var yamlSerializer = Settings.SerializerLibraries.HasFlag(SerializerLibraries.YamlDotNet);
            if (Model.IsAbstract) type.TypeAttributes |= System.Reflection.TypeAttributes.Abstract;
            if (Model.Schema.DiscriminatorObject is OpenApiDiscriminator discriminator)
            {
                if (jsonSerializer || yamlSerializer)
                {
                    if (jsonSerializer)
                    {
                        type.CustomAttributes.Add(new CodeAttributeDeclaration(
                            new CodeTypeReference(typeof(JsonConverter)),
                            new CodeAttributeArgument(new CodeTypeOfExpression(nameof(JsonInheritanceConverter))),
                            new CodeAttributeArgument(new CodePrimitiveExpression(discriminator.PropertyName))));
                    }
                    if (yamlSerializer)
                    {
                        type.CustomAttributes.Add(new CodeAttributeDeclaration(
                            new CodeTypeReference("YamlDiscriminator"),
                            new CodeAttributeArgument(new CodePrimitiveExpression(discriminator.PropertyName))));
                    }

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

            var propertyCount = 0;
            var defaultConstructor = new CodeConstructor { Attributes = Model.IsAbstract ? MemberAttributes.Family : MemberAttributes.Public };
            var modelSchemaProperties = Model.Schema.ActualProperties;
            foreach (var property in Model.Properties)
            {
                propertyCount++;
                modelSchemaProperties.TryGetValue(property.Name, out var propertySchema);
                var isPrimitive = PrimitiveTypes.TryGetValue(property.Type, out string? underlyingType);
                var fieldDeclaration = new CodeMemberField(
                    isPrimitive ? underlyingType : property.Type,
                    property.FieldName);
                if (property.HasDefaultValue)
                {
                    defaultConstructor.Statements.Add(new CodeAssignStatement(
                        new CodeVariableReferenceExpression(property.FieldName),
                        new CodeSnippetExpression(property.DefaultValue)));
                }
                else if (propertySchema?.Default is JObject jsonObject)
                {
                    var targetObject = new CodeVariableReferenceExpression(property.FieldName);
                    BuildDefaultPropertyInitializer(
                        defaultConstructor,
                        targetObject,
                        fieldDeclaration.Type,
                        propertySchema,
                        jsonObject);
                }
                else if (propertySchema?.IsArray is true)
                {
                    defaultConstructor.Statements.Add(new CodeAssignStatement(
                        new CodeVariableReferenceExpression(property.FieldName),
                        new CodeObjectCreateExpression(fieldDeclaration.Type)));
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

                if (jsonSerializer)
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
                                property.IsNullable ? nameof(Required.AllowNull) : nameof(Required.Always))));
                    }
                    propertyDeclaration.CustomAttributes.Add(jsonProperty);
                }
                if (yamlSerializer)
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
                const string CombinatorMethodName = "Generate";
                var combinatorMethod = new CodeMemberMethod
                {
                    Name = CombinatorMethodName,
                    Attributes = MemberAttributes.Public | MemberAttributes.Final,
                    ReturnType = new CodeTypeReference(typeof(IObservable<>))
                    {
                        TypeArguments = { new CodeTypeReference(Model.ClassName) }
                    }
                };
                combinatorMethod.Statements.Add(new CodeMethodReturnStatement(
                    new CodeMethodInvokeExpression(
                        new CodeMethodReferenceExpression(
                            new CodeTypeReferenceExpression("System.Reactive.Linq.Observable"),
                            "Defer"),
                        new CodeSnippetExpression(
                            @$"() => System.Reactive.Linq.Observable.Return(new {Model.ClassName}(this))"))));

                var genericTypeParameter = new CodeTypeParameter("TSource");
                var genericSourceParameter = new CodeParameterDeclarationExpression(new CodeTypeReference(typeof(IObservable<>))
                {
                    TypeArguments = { new CodeTypeReference(genericTypeParameter) }
                }, "source");
                var genericCombinatorMethod = new CodeMemberMethod
                {
                    Name = CombinatorMethodName,
                    Attributes = MemberAttributes.Public | MemberAttributes.Final,
                    TypeParameters = { genericTypeParameter },
                    Parameters = { genericSourceParameter },
                    ReturnType = new CodeTypeReference(typeof(IObservable<>))
                    {
                        TypeArguments = { new CodeTypeReference(Model.ClassName) }
                    }
                };
                genericCombinatorMethod.Statements.Add(new CodeMethodReturnStatement(
                    new CodeMethodInvokeExpression(
                        new CodeMethodReferenceExpression(
                            new CodeTypeReferenceExpression("System.Reactive.Linq.Observable"),
                            "Select"),
                        new CodeVariableReferenceExpression(genericSourceParameter.Name),
                        new CodeSnippetExpression(
                            @$"_ => new {Model.ClassName}(this)"))));

                type.Members.Add(combinatorMethod);
                type.Members.Add(genericCombinatorMethod);
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference("Bonsai.WorkflowElementCategoryAttribute"),
                    new CodeAttributeArgument(new CodeFieldReferenceExpression(
                        new CodeTypeReferenceExpression("Bonsai.ElementCategory"),
                        "Source"))));
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference("Bonsai.CombinatorAttribute"),
                    new CodeAttributeArgument(new CodePrimitiveExpression(CombinatorMethodName))));
            }

            const string PrintMembersMethodName = "PrintMembers";
            const string AppendMethodName = nameof(StringBuilder.Append);
            var stringBuilderParameter = new CodeParameterDeclarationExpression(typeof(StringBuilder), "stringBuilder");
            var stringBuilderVariable = new CodeVariableReferenceExpression(stringBuilderParameter.Name);
            var printMembersMethod = new CodeMemberMethod
            {
                Name = PrintMembersMethodName,
                Attributes = Model.BaseClass != null
                    ? MemberAttributes.Family | MemberAttributes.Override
                    : MemberAttributes.Family,
                Parameters = { stringBuilderParameter },
                ReturnType = new CodeTypeReference(typeof(bool))
            };
            if (Model.BaseClass != null && propertyCount == 0)
            {
                printMembersMethod.Statements.Add(new CodeMethodReturnStatement(
                    new CodeMethodInvokeExpression(new CodeBaseReferenceExpression(), PrintMembersMethodName, stringBuilderVariable)));
            }
            else
            {
                if (Model.BaseClass != null)
                {
                    printMembersMethod.Statements.Add(new CodeConditionStatement(
                        new CodeMethodInvokeExpression(new CodeBaseReferenceExpression(), PrintMembersMethodName, stringBuilderVariable),
                        new CodeExpressionStatement(
                            new CodeMethodInvokeExpression(stringBuilderVariable, AppendMethodName, new CodePrimitiveExpression(", ")))));
                }

                var propertyIndex = 0;
                foreach (var property in Model.Properties)
                {
                    printMembersMethod.Statements.Add(new CodeMethodInvokeExpression(
                        stringBuilderVariable,
                        AppendMethodName,
                        new CodeSnippetExpression(
                            $"\"{property.PropertyName} = \" + {property.FieldName}" +
                            (++propertyIndex < propertyCount ? " + \", \"" : string.Empty))));
                }
                printMembersMethod.Statements.Add(new CodeMethodReturnStatement(new CodePrimitiveExpression(propertyCount > 0)));
            }

            type.Members.Add(printMembersMethod);
            if (Model.BaseClass == null)
            {
                var toStringMethod = new CodeMemberMethod
                {
                    Name = nameof(ToString),
                    Attributes = MemberAttributes.Public | MemberAttributes.Override,
                    ReturnType = new CodeTypeReference(typeof(string)),
                    Statements =
                    {
                        new CodeVariableDeclarationStatement(
                            stringBuilderParameter.Type,
                            stringBuilderParameter.Name,
                            new CodeObjectCreateExpression(stringBuilderParameter.Type)),
                        new CodeMethodInvokeExpression(
                            stringBuilderVariable,
                            AppendMethodName,
                            new CodePropertyReferenceExpression(
                                new CodeMethodInvokeExpression(null, nameof(GetType)),
                                nameof(Type.Name))),
                        new CodeMethodInvokeExpression(stringBuilderVariable, AppendMethodName, new CodePrimitiveExpression(" { ")),
                        new CodeConditionStatement(
                            new CodeMethodInvokeExpression(null, printMembersMethod.Name, stringBuilderVariable),
                            new CodeExpressionStatement(
                                new CodeMethodInvokeExpression(stringBuilderVariable, AppendMethodName, new CodePrimitiveExpression(" ")))),
                        new CodeMethodInvokeExpression(stringBuilderVariable, AppendMethodName, new CodePrimitiveExpression("}")),
                        new CodeMethodReturnStatement(
                            new CodeMethodInvokeExpression(stringBuilderVariable, nameof(ToString)))
                    }
                };
                type.Members.Add(toStringMethod);
            }
        }

        private void BuildDefaultPropertyInitializer(
            CodeConstructor defaultConstructor,
            CodeExpression targetObject,
            CodeTypeReference targetType,
            JsonSchemaProperty propertySchema,
            JObject jsonObject)
        {
            defaultConstructor.Statements.Add(new CodeAssignStatement(
                targetObject,
                new CodeObjectCreateExpression(targetType)));

            var innerSchemaProperties = propertySchema.ActualProperties;
            foreach (var jsonProperty in jsonObject.Properties())
            {
                if (!innerSchemaProperties.TryGetValue(
                    jsonProperty.Name,
                    out JsonSchemaProperty? defaultPropertySchema))
                    continue;

                var defaultProperty = new PropertyModel(Model, defaultPropertySchema, Model.Resolver, Settings);
                if (jsonProperty.Value is JObject nestedObject)
                {
                    var nestedTarget = new CodePropertyReferenceExpression(targetObject, defaultProperty.PropertyName);
                    BuildDefaultPropertyInitializer(
                        defaultConstructor,
                        nestedTarget,
                        new CodeTypeReference(defaultProperty.Type),
                        defaultPropertySchema,
                        nestedObject);
                }

                if (jsonProperty.Value is not JValue jsonValue)
                    continue;

                var schemaDefault = defaultPropertySchema.Default;
                try
                {
                    defaultPropertySchema.Default = jsonValue.Value;
                    var defaultValue = Settings.ValueGenerator.GetDefaultValue(
                        defaultPropertySchema,
                        defaultProperty.IsNullable,
                        defaultProperty.Type,
                        defaultProperty.Name,
                        Settings.GenerateDefaultValues,
                        Model.Resolver);
                    defaultConstructor.Statements.Add(new CodeAssignStatement(
                        new CodePropertyReferenceExpression(targetObject, defaultProperty.PropertyName),
                        new CodeSnippetExpression(defaultValue)));
                }
                finally { defaultPropertySchema.Default = schemaDefault; }
            }
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
