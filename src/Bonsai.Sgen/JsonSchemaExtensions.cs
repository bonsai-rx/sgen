using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NJsonSchema;
using NJsonSchema.References;
using NJsonSchema.Visitors;

namespace Bonsai.Sgen
{
    internal static class JsonSchemaExtensions
    {
        public const string TypeNameAnnotation = "x-sgen-typename";

        public static bool TryGetExternalTypeName(this JsonSchema schema, out string typeName)
        {
            if (schema.ExtensionData?.TryGetValue(TypeNameAnnotation, out object? value) is true &&
                value is string annotationValue)
            {
                typeName = annotationValue;
                return true;
            }

            typeName = string.Empty;
            return false;
        }

        public static JsonSchema WithCompatibleDefinitions(this JsonSchema schema, ITypeNameGenerator typeNameGenerator)
        {
            var schemaAppender = new JsonSchemaAppender(schema, typeNameGenerator);
            var referenceResolver = new JsonReferenceResolver(schemaAppender);
            var definitionVisitor = new DefinitionSchemaVisitor(schema, referenceResolver);
            definitionVisitor.Visit(schema);
            return schema;
        }

        public static JsonSchema WithResolvedDiscriminatorInheritance(this JsonSchema schema)
        {
            var discriminatorVisitor = new DiscriminatorSchemaVisitor(schema);
            var derivedDiscriminatorVisitor = new DerivedDiscriminatorSchemaVisitor();
            discriminatorVisitor.Visit(schema);
            derivedDiscriminatorVisitor.Visit(schema);
            return schema;
        }

        class DiscriminatorSchemaVisitor : JsonSchemaVisitorBase
        {
            readonly Dictionary<JsonSchema, string> definitionTypeNameLookup = new();

            public DiscriminatorSchemaVisitor(JsonSchema rootObject)
            {
                RootObject = rootObject;
                VisitDefinitions(rootObject);
            }

            public JsonSchema RootObject { get; }

            private void ResolveOneOfInheritance(JsonSchema schema, JsonSchema baseSchema)
            {
                foreach (var derivedSchema in schema.OneOf)
                {
                    if (derivedSchema.IsNullable(SchemaType.JsonSchema))
                    {
                        continue;
                    }

                    var actualSchema = derivedSchema.ActualSchema;
                    if (!actualSchema.AllOf.Any(schema => schema.Reference == baseSchema))
                    {
                        actualSchema.AllOf.Add(new JsonSchema { Reference = baseSchema });
                    }
                }
            }

            protected override JsonSchema VisitSchema(JsonSchema schema, string path, string? typeNameHint)
            {
                var actualSchema = schema.ActualSchema;
                if (actualSchema.DiscriminatorObject != null)
                {
                    var isDefinition = definitionTypeNameLookup.TryGetValue(actualSchema, out _);
                    if (schema is JsonSchemaProperty || schema.ParentSchema?.Item == schema || isDefinition)
                    {
                        var discriminatorSchema = isDefinition ? actualSchema : null;
                        if (string.IsNullOrEmpty(typeNameHint))
                        {
                            typeNameHint = "Anonymous";
                        }

                        if (discriminatorSchema == null && !RootObject.Definitions.TryGetValue(typeNameHint, out discriminatorSchema))
                        {
                            discriminatorSchema = new JsonSchema();
                            discriminatorSchema.DiscriminatorObject = actualSchema.DiscriminatorObject;
                            discriminatorSchema.IsAbstract = actualSchema.IsAbstract;
                            RootObject.Definitions.Add(typeNameHint, discriminatorSchema);
                            ResolveOneOfInheritance(actualSchema, discriminatorSchema);
                        }
                        else
                        {
                            if (discriminatorSchema.OneOf.Count > 0)
                            {
                                ResolveOneOfInheritance(discriminatorSchema, discriminatorSchema);
                            }
                        }

                        if (!isDefinition)
                        {
                            actualSchema.DiscriminatorObject = null;
                            actualSchema.IsAbstract = false;
                        }
                    }
                }

                return schema;
            }

            private void VisitDefinitions(JsonSchema schema)
            {
                if (schema == null ||
                    schema.Reference != null)
                {
                    return;
                }

                VisitDefinitions(schema.Item);
                VisitDefinitions(schema.AdditionalItemsSchema);
                VisitDefinitions(schema.AdditionalPropertiesSchema);
                VisitDefinitions(schema.Items);
                VisitDefinitions(schema.AllOf);
                VisitDefinitions(schema.AnyOf);
                VisitDefinitions(schema.OneOf);
                VisitDefinitions(schema.Not);
                VisitDefinitions(schema.DictionaryKey);
                VisitDefinitions(schema.Properties);
                VisitDefinitions(schema.PatternProperties);
                if (schema.Definitions.Count > 0)
                {
                    foreach (var definition in schema.Definitions)
                    {
                        definitionTypeNameLookup[definition.Value] = definition.Key;
                        VisitDefinitions(definition.Value);
                    }
                }
            }

            private void VisitDefinitions(ICollection<JsonSchema> collection)
            {
                if (collection.Count > 0)
                {
                    foreach (var schema in collection)
                    {
                        VisitDefinitions(schema);
                    }
                }
            }

            private void VisitDefinitions(IDictionary<string, JsonSchemaProperty> dictionary)
            {
                if (dictionary.Count > 0)
                {
                    foreach (var schema in dictionary.Values)
                    {
                        VisitDefinitions(schema);
                    }
                }
            }
        }

        class DerivedDiscriminatorSchemaVisitor : JsonSchemaVisitorBase
        {
            protected override JsonSchema VisitSchema(JsonSchema schema, string path, string typeNameHint)
            {
                foreach (var baseSchema in schema.AllInheritedSchemas)
                {
                    var discriminatorSchema = baseSchema.DiscriminatorObject;
                    if (discriminatorSchema != null)
                    {
                        schema.Properties.Remove(discriminatorSchema.PropertyName);
                    }
                }

                return schema;
            }
        }

        class DefinitionSchemaVisitor : JsonSchemaVisitorBase
        {
            const string DefsExtension = "$defs";

            public DefinitionSchemaVisitor(object rootObject, JsonReferenceResolver referenceResolver)
            {
                RootObject = rootObject;
                ReferenceResolver = referenceResolver;
                ContractResolver = new DefaultContractResolver();
            }

            public object RootObject { get; }

            public JsonReferenceResolver ReferenceResolver { get; }

            public IContractResolver ContractResolver { get; }

            protected override IJsonReference VisitJsonReference(IJsonReference reference, string path, string typeNameHint)
            {
                if (reference.ReferencePath != null && reference.Reference == null)
                {
                    reference.Reference = ReferenceResolver.ResolveReferenceAsync(
                        RootObject,
                        reference.ReferencePath,
                        reference.GetType(),
                        ContractResolver).Result;
                }

                return base.VisitJsonReference(reference, path, typeNameHint);
            }

            protected override JsonSchema VisitSchema(JsonSchema schema, string path, string typeNameHint)
            {
                if (schema.ExtensionData?.TryGetValue(DefsExtension, out var defs) == true &&
                    defs is IDictionary<string, object> definitions)
                {
                    foreach (var entry in definitions)
                    {
                        JsonSchema definition;
                        if (entry.Value is IDictionary dictionary)
                        {
                            var settings = new JsonSerializerSettings { ContractResolver = ContractResolver };
                            var json = JsonConvert.SerializeObject(dictionary, settings);
                            definition = JsonConvert.DeserializeObject<JsonSchema>(json, settings) ??
                                throw new InvalidOperationException(
                                    $"Unable to resolve definition {entry.Key} within JSON path '{path}'.");
                        }
                        else definition = (JsonSchema)entry.Value;
                        schema.Definitions.Add(entry.Key, definition);
                    }
                }

                return schema;
            }
        }
    }
}
