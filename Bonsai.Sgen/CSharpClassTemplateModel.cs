using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;
using NJsonSchema.CodeGeneration.CSharp.Models;

namespace Bonsai.Sgen
{
    internal class CSharpClassTemplateModel : ClassTemplateModel
    {
        public CSharpClassTemplateModel(
            string typeName,
            CSharpGeneratorSettings settings,
            CSharpTypeResolver resolver,
            JsonSchema schema,
            object rootObject)
            : base(typeName, settings, resolver, schema, rootObject)
        {
            Schema = schema;
            Resolver = resolver;
        }

        public JsonSchema Schema { get; }

        public CSharpTypeResolver Resolver { get; }
    }
}
