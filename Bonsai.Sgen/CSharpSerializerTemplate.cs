using System.CodeDom;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Xml.Serialization;
using NJsonSchema;
using NJsonSchema.CodeGeneration;

namespace Bonsai.Sgen
{
    internal abstract class CSharpSerializerTemplate : ITemplate
    {
        public CSharpSerializerTemplate(
            IEnumerable<CodeArtifact> modelTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
        {
            ModelTypes = modelTypes;
            Provider = provider;
            Options = options;
            Settings = settings;
        }

        public IEnumerable<CodeArtifact> ModelTypes { get; }

        public CodeDomProvider Provider { get; }

        public CodeGeneratorOptions Options { get; }

        public CSharpCodeDomGeneratorSettings Settings { get; }

        public string Render()
        {
            var type = new CodeTypeDeclaration(ClassName) { IsPartial = true };
            RenderType(type);
            var description = Description;
            if (!string.IsNullOrEmpty(description))
            {
                type.Comments.Add(new CodeCommentStatement("<summary>", docComment: true));
                type.Comments.Add(new CodeCommentStatement(description, docComment: true));
                type.Comments.Add(new CodeCommentStatement("</summary>", docComment: true));
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference(typeof(DescriptionAttribute)),
                    new CodeAttributeArgument(new CodePrimitiveExpression(description))));
            }

            using var writer = new StringWriter();
            Provider.GenerateCodeFromType(type, writer, Options);
            return writer.ToString();
        }

        public abstract string ClassName { get; }

        public abstract string Description { get; }

        public abstract void RenderType(CodeTypeDeclaration type);
    }

    internal class CSharpJsonSerializerTemplate : CSharpSerializerTemplate
    {
        public CSharpJsonSerializerTemplate(
            IEnumerable<CodeArtifact> modelTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(modelTypes, provider, options, settings)
        {
        }

        public override string ClassName => "SerializeToJson";

        public override string Description => "Serializes a sequence of data model objects into JSON strings.";

        public override void RenderType(CodeTypeDeclaration type)
        {
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference("Bonsai.CombinatorAttribute")));
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference("Bonsai.WorkflowElementCategoryAttribute"),
                new CodeAttributeArgument(new CodeFieldReferenceExpression(
                    new CodeTypeReferenceExpression("Bonsai.ElementCategory"),
                    "Transform"))));
            type.Members.Add(new CodeSnippetTypeMember(
@"    private System.IObservable<string> Process<T>(System.IObservable<T> source)
    {
        return System.Reactive.Linq.Observable.Select(source, value => Newtonsoft.Json.JsonConvert.SerializeObject(value));
    }"));
            foreach (var modelType in ModelTypes)
            {
                type.Members.Add(new CodeSnippetTypeMember(@$"
    public System.IObservable<string> Process(System.IObservable<{modelType.TypeName}> source)
    {{
        return Process<{modelType.TypeName}>(source);
    }}"));
            }
        }
    }

    internal class CSharpYamlSerializerTemplate : CSharpSerializerTemplate
    {
        public CSharpYamlSerializerTemplate(
            IEnumerable<CodeArtifact> modelTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(modelTypes, provider, options, settings)
        {
        }

        public override string ClassName => "SerializeToYaml";

        public override string Description => "Serializes a sequence of data model objects into YAML strings.";

        public override void RenderType(CodeTypeDeclaration type)
        {
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference("Bonsai.CombinatorAttribute")));
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference("Bonsai.WorkflowElementCategoryAttribute"),
                new CodeAttributeArgument(new CodeFieldReferenceExpression(
                    new CodeTypeReferenceExpression("Bonsai.ElementCategory"),
                    "Transform"))));
            type.Members.Add(new CodeSnippetTypeMember(
@"    private System.IObservable<string> Process<T>(System.IObservable<T> source)
    {
        return System.Reactive.Linq.Observable.Defer(() =>
        {
            var serializer = new YamlDotNet.Serialization.SerializerBuilder().Build();
            return System.Reactive.Linq.Observable.Select(source, value => serializer.Serialize(value)); 
        });
    }"));
            foreach (var modelType in ModelTypes)
            {
                type.Members.Add(new CodeSnippetTypeMember(@$"
    public System.IObservable<string> Process(System.IObservable<{modelType.TypeName}> source)
    {{
        return Process<{modelType.TypeName}>(source);
    }}"));
            }
        }
    }

    internal abstract class CSharpDeserializerTemplate : CSharpSerializerTemplate
    {
        readonly JsonSchema _schema;

        public CSharpDeserializerTemplate(
            JsonSchema schema,
            IEnumerable<CodeArtifact> modelTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(modelTypes, provider, options, settings)
        {
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        public override void RenderType(CodeTypeDeclaration type)
        {
            type.BaseTypes.Add(new CodeTypeReference("Bonsai.Expressions.SingleArgumentExpressionBuilder"));
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference(typeof(DefaultPropertyAttribute)),
                new CodeAttributeArgument(new CodePrimitiveExpression("Type"))));
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference("Bonsai.WorkflowElementCategoryAttribute"),
                new CodeAttributeArgument(new CodeFieldReferenceExpression(
                    new CodeTypeReferenceExpression("Bonsai.ElementCategory"),
                    "Transform"))));
            foreach (var modelType in ModelTypes)
            {
                type.CustomAttributes.Add(new CodeAttributeDeclaration(
                    new CodeTypeReference(typeof(XmlIncludeAttribute)),
                    new CodeAttributeArgument(new CodeTypeOfExpression(
                        new CodeTypeReference(
                            "Bonsai.Expressions.TypeMapping",
                            new CodeTypeReference(modelType.TypeName))))));
            }

            type.Members.Add(new CodeSnippetTypeMember(
@$"    public {ClassName}()
    {{
        Type = new Bonsai.Expressions.TypeMapping<{_schema.Title}>();
    }}

    public Bonsai.Expressions.TypeMapping Type {{ get; set; }}

    public override System.Linq.Expressions.Expression Build(System.Collections.Generic.IEnumerable<System.Linq.Expressions.Expression> arguments)
    {{
        var typeMapping = (Bonsai.Expressions.TypeMapping)Type;
        var returnType = typeMapping.GetType().GetGenericArguments()[0];
        return System.Linq.Expressions.Expression.Call(
            typeof({ClassName}),
            ""Process"",
            new System.Type[] {{ returnType }},
            System.Linq.Enumerable.Single(arguments));
    }}
"));
        }
    }

    internal class CSharpYamlDeserializerTemplate : CSharpDeserializerTemplate
    {
        public CSharpYamlDeserializerTemplate(
            JsonSchema schema,
            IEnumerable<CodeArtifact> modelTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(schema, modelTypes, provider, options, settings)
        {
        }

        public override string ClassName => "DeserializeFromYaml";

        public override string Description => "Deserializes a sequence of YAML strings into data model objects.";

        public override void RenderType(CodeTypeDeclaration type)
        {
            base.RenderType(type);
            type.Members.Add(new CodeSnippetTypeMember(
@"    private static System.IObservable<T> Process<T>(System.IObservable<string> source)
    {
        return System.Reactive.Linq.Observable.Defer(() =>
        {
            var serializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
            return System.Reactive.Linq.Observable.Select(source, value =>
            {
                var reader = new System.IO.StringReader(value);
                var parser = new YamlDotNet.Core.MergingParser(new YamlDotNet.Core.Parser(reader));
                return serializer.Deserialize<T>(parser);
            });
        });
    }"));
        }
    }

    internal class CSharpJsonDeserializerTemplate : CSharpDeserializerTemplate
    {
        public CSharpJsonDeserializerTemplate(
            JsonSchema schema,
            IEnumerable<CodeArtifact> modelTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(schema, modelTypes, provider, options, settings)
        {
        }

        public override string ClassName => "DeserializeFromJson";

        public override string Description => "Deserializes a sequence of JSON strings into data model objects.";

        public override void RenderType(CodeTypeDeclaration type)
        {
            base.RenderType(type);
            type.Members.Add(new CodeSnippetTypeMember(
@"    private static System.IObservable<T> Process<T>(System.IObservable<string> source)
    {
        return System.Reactive.Linq.Observable.Select(source, value => Newtonsoft.Json.JsonConvert.DeserializeObject<T>(value));
    }"));
        }
    }
}
