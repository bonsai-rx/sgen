using System;
using System.Collections.Generic;
using NJsonSchema;

namespace Bonsai.Sgen.Tests
{
    static class SchemaTestHelper
    {
        public static JsonSchema CreateContainerSchema(IEnumerable<KeyValuePair<string, JsonSchema>> definitions)
        {
            var result = new JsonSchema
            {
                Type = JsonObjectType.Object,
                Title = "Container"
            };
            foreach (var definition in definitions)
            {
                result.Definitions.Add(definition);
            }
            return result;
        }

        public static void AddRange(this ICollection<JsonSchema> collection, IEnumerable<JsonSchema> schemas)
        {
            foreach (var schema in schemas)
            {
                collection.Add(schema);
            }
        }

        public static JsonSchema CreateNullSchema() => new() { Type = JsonObjectType.Null };

        public static JsonSchema CreateOneOfSchema(IEnumerable<JsonSchema> schemas, bool optional = true)
        {
            var schema = new JsonSchema();
            schema.OneOf.AddRange(schemas);
            if (optional)
            {
                schema.OneOf.Add(CreateNullSchema());
            }
            return schema;
        }

        public static JsonSchema CreateDiscriminatorSchema(
            string propertyName = "kind",
            params KeyValuePair<string, JsonSchema>[] mappings)
        {
            return CreateDiscriminatorSchema<JsonSchema>(propertyName, mappings);
        }

        public static TSchemaType CreateDiscriminatorSchema<TSchemaType>(
            string propertyName = "kind",
            params KeyValuePair<string, JsonSchema>[] mappings)
            where TSchemaType : JsonSchema, new()
        {
            var discriminator = new OpenApiDiscriminator { PropertyName = propertyName };
            var result = new TSchemaType()
            {
                IsAbstract = true,
                DiscriminatorObject = discriminator
            };

            if (mappings.Length > 0)
            {
                for (int i = 0; i < mappings.Length; i++)
                {
                    discriminator.Mapping.Add(mappings[i]);
                    result.OneOf.Add(new JsonSchema { Reference = mappings[i].Value });
                }

                result.OneOf.Add(new JsonSchema { Type = JsonObjectType.Null });
            }
            return result;
        }

        public static KeyValuePair<string, JsonSchema>[] CreateDerivedSchemas(string propertyName, params string[] keys)
        {
            return CreateDerivedSchemas(propertyName, baseSchema: null, keys);
        }

        public static KeyValuePair<string, JsonSchema>[] CreateDerivedSchemas(string propertyName, JsonSchema? baseSchema, params string[] keys)
        {
            return Array.ConvertAll(keys, key =>
            {
                var schema = new JsonSchema()
                {
                    Type = JsonObjectType.Object,
                    RequiredProperties = { propertyName },
                    Properties =
                    {
                        { propertyName, new JsonSchemaProperty { Enumeration = { key } } }
                    }
                };
                if (baseSchema != null) schema.AllOf.Add(baseSchema);
                return new KeyValuePair<string, JsonSchema>(key, schema);
            });
        }
    }
}
