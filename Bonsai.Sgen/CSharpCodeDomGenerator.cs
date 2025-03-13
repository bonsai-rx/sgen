using System.CodeDom.Compiler;
using Microsoft.CSharp;
using NJsonSchema;
using NJsonSchema.CodeGeneration;
using NJsonSchema.CodeGeneration.CSharp;
using NJsonSchema.CodeGeneration.CSharp.Models;
using NJsonSchema.Converters;

namespace Bonsai.Sgen
{
    internal class CSharpCodeDomGenerator : CSharpGenerator
    {
        private readonly CSharpTypeResolver _resolver;
        private readonly CodeDomProvider _provider;
        private readonly CodeGeneratorOptions _options;

        public CSharpCodeDomGenerator(object rootObject)
            : this(rootObject, new CSharpCodeDomGeneratorSettings())
        {
        }

        public CSharpCodeDomGenerator(object rootObject, CSharpCodeDomGeneratorSettings settings)
            : this(rootObject, settings, new CSharpTypeResolver(settings))
        {
        }

        public CSharpCodeDomGenerator(object rootObject, CSharpCodeDomGeneratorSettings settings, CSharpTypeResolver resolver)
            : base(rootObject, settings, resolver)
        {
            _resolver = resolver;
            _provider = new CSharpCodeProvider();
            _options = new CodeGeneratorOptions { BracingStyle = "C" };
            Settings = settings;
        }

        public new CSharpCodeDomGeneratorSettings Settings { get; }

        protected override CodeArtifact GenerateType(JsonSchema schema, string typeNameHint)
        {
            if (schema.TryGetExternalTypeName(out var _))
                return new CodeArtifact(typeNameHint, default, default, default, string.Empty);

            var typeName = _resolver.GetOrGenerateTypeName(schema, typeNameHint);
            if (schema.IsEnumeration)
            {
                return GenerateEnum(schema, typeName);
            }
            else
            {
                return GenerateClass(schema, typeName);
            };
        }

        private CodeArtifact GenerateClass(JsonSchema schema, string typeName)
        {
            var model = new CSharpClassTemplateModel(typeName, Settings, _resolver, schema, RootObject);
            var template = new CSharpClassTemplate(model, _provider, _options, Settings);
            return new CSharpClassCodeArtifact(model, template);
        }

        private CodeArtifact GenerateClass(CSharpCodeDomTemplate template)
        {
            return new CodeArtifact(template.TypeName, CodeArtifactType.Class, CodeArtifactLanguage.CSharp, CodeArtifactCategory.Contract, template);
        }

        private CodeArtifact GenerateEnum(JsonSchema schema, string typeName)
        {
            var model = new EnumTemplateModel(typeName, schema, Settings);
            var template = new CSharpEnumTemplate(model, _provider, _options, Settings);
            return new CodeArtifact(typeName, CodeArtifactType.Enum, CodeArtifactLanguage.CSharp, CodeArtifactCategory.Contract, template);
        }

        private CodeArtifact ReplaceInitOnlyProperties(CodeArtifact modelType)
        {
            if (modelType.TypeName == nameof(JsonInheritanceAttribute))
            {
                var code = modelType.Code.Replace("{ get; }", "{ get; private set; }");
                modelType = new CodeArtifact(modelType.TypeName, modelType.Type, modelType.Language, modelType.Category, code);
            }

            return modelType;
        }

        public override IEnumerable<CodeArtifact> GenerateTypes()
        {
            if (RootObject is JsonSchema jsonSchema)
                _resolver.RegisterSchemaDefinitions(jsonSchema.Definitions);

            var types = base.GenerateTypes();
            var extraTypes = new List<CodeArtifact>();

            if (!Settings.SerializerLibraries.HasFlag(SerializerLibraries.NewtonsoftJson) &&
                types.Any(r => r.Code.Contains(nameof(JsonInheritanceAttribute))))
            {
                if (Settings.ExcludedTypeNames?.Contains(nameof(JsonInheritanceAttribute)) != true)
                {
                    var model = new JsonInheritanceConverterTemplateModel(Settings);
                    var template = Settings.TemplateFactory.CreateTemplate("CSharp", nameof(JsonInheritanceAttribute), model);
                    var artifact = new CodeArtifact(
                        nameof(JsonInheritanceAttribute),
                        CodeArtifactType.Class,
                        CodeArtifactLanguage.CSharp,
                        CodeArtifactCategory.Utility,
                        template);
                    extraTypes.Add(ReplaceInitOnlyProperties(artifact));
                }
            }

            var schema = (JsonSchema)RootObject;
            var classTypes = (from type in types
                              let classType = type as CSharpClassCodeArtifact
                              where classType != null
                              select classType).ToList();
            var discriminatorTypes = classTypes.Where(modelType => modelType.Model.Schema.DiscriminatorObject != null).ToList();
            foreach (var type in discriminatorTypes)
            {
                var matchTemplate = new CSharpTypeMatchTemplate(type, _provider, _options, Settings);
                extraTypes.Add(GenerateClass(matchTemplate));
            }
            if (Settings.SerializerLibraries.HasFlag(SerializerLibraries.NewtonsoftJson) && classTypes.Count > 0)
            {
                var serializer = new CSharpJsonSerializerTemplate(classTypes, _provider, _options, Settings);
                var deserializer = new CSharpJsonDeserializerTemplate(schema, classTypes, _provider, _options, Settings);
                extraTypes.Add(GenerateClass(serializer));
                extraTypes.Add(GenerateClass(deserializer));
            }
            if (Settings.SerializerLibraries.HasFlag(SerializerLibraries.YamlDotNet) && classTypes.Count > 0)
            {
                if (discriminatorTypes.Count > 0)
                {
                    var discriminator = new CSharpYamlDiscriminatorTemplate(_provider, _options, Settings);
                    var typeInspector = new CSharpYamlDiscriminatorTypeInspectorTemplate(_provider, _options, Settings);
                    extraTypes.Add(GenerateClass(discriminator));
                    extraTypes.Add(GenerateClass(typeInspector));
                }
                var serializer = new CSharpYamlSerializerTemplate(classTypes, discriminatorTypes, _provider, _options, Settings);
                var deserializer = new CSharpYamlDeserializerTemplate(schema, classTypes, discriminatorTypes, _provider, _options, Settings);
                extraTypes.Add(GenerateClass(serializer));
                extraTypes.Add(GenerateClass(deserializer));
            }

            return types
                .Where(type => type.Type != CodeArtifactType.Undefined)
                .Select(ReplaceInitOnlyProperties)
                .Concat(extraTypes);
        }
    }
}
