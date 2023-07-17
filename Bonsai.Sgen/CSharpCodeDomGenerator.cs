﻿using System.CodeDom.Compiler;
using Microsoft.CSharp;
using NJsonSchema;
using NJsonSchema.CodeGeneration;
using NJsonSchema.CodeGeneration.CSharp;
using NJsonSchema.CodeGeneration.CSharp.Models;

namespace Bonsai.Sgen
{
    internal class CSharpCodeDomGenerator : CSharpGenerator
    {
        private CSharpTypeResolver _resolver;
        private CodeDomProvider _provider;
        private CodeGeneratorOptions _options;

        public CSharpCodeDomGenerator(object rootObject)
            : this(rootObject, new CSharpGeneratorSettings())
        {
        }

        public CSharpCodeDomGenerator(object rootObject, CSharpGeneratorSettings settings)
            : this(rootObject, settings, new CSharpTypeResolver(settings))
        {
        }

        public CSharpCodeDomGenerator(object rootObject, CSharpGeneratorSettings settings, CSharpTypeResolver resolver)
            : base(rootObject, settings, resolver)
        {
            _resolver = resolver;
            _provider = new CSharpCodeProvider();
            _options = new CodeGeneratorOptions { BracingStyle = "C" };
        }

        protected override CodeArtifact GenerateType(JsonSchema schema, string typeNameHint)
        {
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
            var model = new ClassTemplateModel(typeName, Settings, _resolver, schema, RootObject);
            var template = new CSharpClassTemplate(model, _provider, _options);
            return new CodeArtifact(typeName, model.BaseClassName, CodeArtifactType.Class, CodeArtifactLanguage.CSharp, CodeArtifactCategory.Contract, template);
        }

        private CodeArtifact GenerateEnum(JsonSchema schema, string typeName)
        {
            var model = new EnumTemplateModel(typeName, schema, Settings);
            var template = new CSharpEnumTemplate(model, _provider, _options);
            return new CodeArtifact(typeName, CodeArtifactType.Enum, CodeArtifactLanguage.CSharp, CodeArtifactCategory.Contract, template);
        }
    }
}