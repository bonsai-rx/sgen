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
            RootObject = rootObject;
        }

        public JsonSchema Schema { get; }

        public CSharpTypeResolver Resolver { get; }

        public object RootObject { get; }

        public IEnumerable<UnionWrapperModel> GetUnionWrappers()
        {
            if (Schema.DiscriminatorObject == null || RootObject is not JsonSchema rootSchema)
                yield break;

            foreach (var definition in rootSchema.Definitions)
            {
                var wrapperSchema = definition.Value.ActualSchema;
                if (!wrapperSchema.TryGetUnionWrapperTag(out _) ||
                    wrapperSchema.InheritedSchema?.ActualSchema != Schema.ActualSchema)
                    continue;

                var valueSchema = wrapperSchema.Properties[JsonSchemaExtensions.UnionWrapperValueProperty];
                yield return new UnionWrapperModel(
                    Resolver.GetOrGenerateTypeName(wrapperSchema, definition.Key),
                    Resolver.Resolve(valueSchema, false, JsonSchemaExtensions.UnionWrapperValueProperty));
            }
        }
    }

    internal record UnionWrapperModel(string TypeName, string ValueTypeName);
}
