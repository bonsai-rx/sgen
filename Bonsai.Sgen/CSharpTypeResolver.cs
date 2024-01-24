using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;

namespace Bonsai.Sgen
{
    internal class CSharpTypeResolver : NJsonSchema.CodeGeneration.CSharp.CSharpTypeResolver
    {
        readonly Dictionary<JsonSchema, JsonSchema> _baseTypeCache = new();

        public CSharpTypeResolver(CSharpGeneratorSettings settings)
            : base(settings)
        {
        }

        public override JsonSchema RemoveNullability(JsonSchema schema)
        {
            JsonSchema? selectedSchema = null;
            foreach (JsonSchema o in schema.ActualSchema.OneOf)
            {
                if (o.IsNullable(SchemaType.JsonSchema))
                {
                    continue;
                }

                if (selectedSchema == null)
                {
                    selectedSchema = o;
                }
                else
                {
                    return ResolveBaseTypeSchema(schema.ActualSchema);
                }
            }

            return selectedSchema ?? schema;
        }

        private JsonSchema ResolveBaseTypeSchema(JsonSchema schema)
        {
            if (!_baseTypeCache.TryGetValue(schema, out JsonSchema? baseSchema))
            {
                foreach (JsonSchema o in schema.OneOf)
                {
                    if (o.IsNullable(SchemaType.JsonSchema))
                    {
                        continue;
                    }

                    if (baseSchema == null)
                    {
                        baseSchema = o;
                    }
                    else
                    {
                        baseSchema = FindBestBaseSchema(baseSchema, o);
                        if (baseSchema == null) break;
                    }
                }

                baseSchema ??= JsonSchema.CreateAnySchema();
                _baseTypeCache[schema] = baseSchema;
            }

            return baseSchema;
        }

        private static JsonSchema? FindBestBaseSchema(JsonSchema baseSchema, JsonSchema schema)
        {
            while (!IsAssignableFrom(baseSchema.ActualSchema, schema))
            {
                baseSchema = baseSchema.ActualSchema.InheritedSchema;
                if (baseSchema == null) break;
            }

            return baseSchema;
        }

        private static bool IsAssignableFrom(JsonSchema schema, JsonSchema? typeSchema)
        {
            while (typeSchema?.ActualSchema != null && schema != typeSchema.ActualSchema)
            {
                typeSchema = typeSchema.ActualSchema.InheritedSchema;
            }

            return typeSchema?.ActualSchema != null;
        }
    }
}
