﻿using System.CodeDom;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Text;
using System.Xml.Serialization;
using Newtonsoft.Json;
using NJsonSchema;
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
            foreach (var property in Model.Properties)
            {
                propertyCount++;
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
                                nameof(Required.Always))));
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

                var genericTypeParameter = new CodeTypeParameter("TSource");
                var genericSourceParameter = new CodeParameterDeclarationExpression(new CodeTypeReference(typeof(IObservable<>))
                {
                    TypeArguments = { new CodeTypeReference(genericTypeParameter) }
                }, "source");
                var genericProcessMethod = new CodeMemberMethod
                {
                    Name = "Process",
                    Attributes = MemberAttributes.Public | MemberAttributes.Final,
                    TypeParameters = { genericTypeParameter },
                    Parameters = { genericSourceParameter },
                    ReturnType = new CodeTypeReference(typeof(IObservable<>))
                    {
                        TypeArguments = { new CodeTypeReference(Model.ClassName) }
                    }
                };
                genericProcessMethod.Statements.Add(new CodeMethodReturnStatement(
                    new CodeMethodInvokeExpression(
                        new CodeMethodReferenceExpression(
                            new CodeTypeReferenceExpression("System.Reactive.Linq.Observable"),
                            "Select"),
                        new CodeVariableReferenceExpression(genericSourceParameter.Name),
                        new CodeSnippetExpression(
                            @$"_ => new {Model.ClassName}(this)"))));

                type.Members.Add(processMethod);
                type.Members.Add(genericProcessMethod);
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference("Bonsai.CombinatorAttribute")));
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference("Bonsai.WorkflowElementCategoryAttribute"),
                    new CodeAttributeArgument(new CodeFieldReferenceExpression(
                        new CodeTypeReferenceExpression("Bonsai.ElementCategory"),
                        "Source"))));
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
                            $"\"{property.Name} = \" + {property.FieldName}" +
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
    }
}
