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

        public override string Resolve(JsonSchema schema, bool isNullable, string typeNameHint)
        {
            var typeName = base.Resolve(schema, isNullable, typeNameHint);
            if (schema.ActualSchema.IsArray && schema.ActualSchema.UniqueItems && Settings is CSharpCodeDomGeneratorSettings generatorSettings)
            {
                var itemSchema = schema.ActualSchema.Item;
                var itemType = itemSchema != null
                    ? Resolve(itemSchema, itemSchema.IsNullable(Settings.SchemaType), string.Empty)
                    : "object";
                return $"{generatorSettings.SetType}<{itemType}>";
            }
            if (schema.IsDictionary &&
                schema.ExtensionData?.TryGetValue(JsonSchemaExtensions.PropertyNamesSchema, out var value) is true &&
                value is JsonSchema propertyNamesSchema)
            {
                var valueType = ResolveDictionaryValueType(schema, "object");
                var keyType = Resolve(propertyNamesSchema, propertyNamesSchema.ActualSchema.IsNullable(Settings.SchemaType), string.Empty);
                return $"{Settings.DictionaryType}<{keyType}, {valueType}>";
            }

            return typeName;
        }

        protected override bool IsDefinitionTypeSchema(JsonSchema schema)
        {
            var valueSchemas = schema.OneOf.Where(member => !member.IsNullable(SchemaType.JsonSchema)).ToList();
            if (schema.Type == JsonObjectType.None && schema.OneOf.Count == 2 && valueSchemas.Count == 1)
                return base.IsDefinitionTypeSchema(valueSchemas[0].ActualSchema);

            return base.IsDefinitionTypeSchema(schema);
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
