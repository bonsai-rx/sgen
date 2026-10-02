using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;
using NJsonSchema.References;
using NJsonSchema.Visitors;

namespace Bonsai.Sgen
{
    internal static class JsonSchemaExtensions
    {
        public const string TypeNameAnnotation = "x-sgen-typename";
        public const string PropertyNamesSchema = "PropertyNamesSchema";
        const string ConstKeyword = "const";
        public const string UnionWrapperTag = "UnionWrapperTag";
        public const string UnionWrapperValueProperty = "Value";

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

        public static Func<JsonSchema, JsonReferenceResolver> CreateJsonReferenceResolverFactory()
        {
            return CreateJsonReferenceResolverFactory(schema => new DefinitionReferenceResolver(
                new JsonSchemaAppender(schema, new DefaultTypeNameGenerator())));
        }

        public static Func<JsonSchema, JsonReferenceResolver> CreateJsonReferenceResolverFactory(
            Func<JsonSchema, JsonReferenceResolver> referenceResolverFactory)
        {
            return schema =>
            {
                DefinitionSchemaVisitor.ResolveDefinitionSchemas(schema, new DefaultContractResolver(), "#");
                return referenceResolverFactory(schema);
            };
        }

        public static JsonSchema WithCompatibleDefinitions(this JsonSchema schema, ITypeNameGenerator typeNameGenerator)
        {
            var schemaAppender = new JsonSchemaAppender(schema, typeNameGenerator);
            var referenceResolver = new JsonReferenceResolver(schemaAppender);
            var definitionVisitor = new DefinitionSchemaVisitor(schema, referenceResolver);
            definitionVisitor.Visit(schema);
            return schema;
        }

        public static JsonSchema WithResolvedAnyOfNullableProperty(this JsonSchema schema)
        {
            var anyOfNullablePropertyVisitor = new AnyOfNullablePropertySchemaVisitor();
            anyOfNullablePropertyVisitor.Visit(schema);
            return schema;
        }

        public static bool TryGetUnionWrapperTag(this JsonSchema schema, out string tag)
        {
            if (schema.ExtensionData?.TryGetValue(UnionWrapperTag, out object? value) is true &&
                value is DiscriminatorTag discriminatorTag)
            {
                tag = discriminatorTag.Value;
                return true;
            }

            tag = string.Empty;
            return false;
        }

        record DiscriminatorTag(string Value);

        public static JsonSchema WithResolvedDiscriminatorInheritance(this JsonSchema schema, CSharpGeneratorSettings settings)
        {
            var discriminatorVisitor = new DiscriminatorSchemaVisitor(schema, settings);
            var derivedDiscriminatorVisitor = new DerivedDiscriminatorSchemaVisitor();
            discriminatorVisitor.Visit(schema);
            derivedDiscriminatorVisitor.Visit(schema);
            return schema;
        }

        class DefinitionReferenceResolver(JsonSchemaAppender schemaAppender) : JsonReferenceResolver(schemaAppender)
        {
            public override async Task<IJsonReference> ResolveFileReferenceAsync(string filePath, CancellationToken cancellationToken = default)
            {
                return await JsonSchema.FromFileAsync(filePath, ResolveDefinitionSchemas, cancellationToken);
            }

            public override async Task<IJsonReference> ResolveUrlReferenceAsync(string url, CancellationToken cancellationToken = default)
            {
                return await JsonSchema.FromUrlAsync(url, ResolveDefinitionSchemas, cancellationToken);
            }

            private JsonReferenceResolver ResolveDefinitionSchemas(JsonSchema schema)
            {
                DefinitionSchemaVisitor.ResolveDefinitionSchemas(schema, new DefaultContractResolver(), "#");
                return this;
            }
        }

        class DiscriminatorSchemaVisitor : JsonSchemaVisitorBase
        {
            readonly Dictionary<JsonSchema, string> definitionTypeNameLookup = new();

            public DiscriminatorSchemaVisitor(JsonSchema rootObject, CSharpGeneratorSettings settings)
            {
                RootObject = rootObject;
                Settings = settings;
                VisitDefinitions(rootObject);
            }

            public JsonSchema RootObject { get; }

            public CSharpGeneratorSettings Settings { get; }

            private bool IsExternal(JsonSchema schema)
            {
                return schema.TryGetExternalTypeName(out var typeName) &&
                    !CSharpTypeNameGenerator.NamespaceEquals(typeName, Settings.Namespace);
            }

            private static bool InheritsOtherType(JsonSchema schema, JsonSchema baseSchema)
            {
                var inheritedSchema = schema.InheritedSchema;
                return inheritedSchema != null && inheritedSchema.ActualSchema != baseSchema;
            }

            private void ResolveOneOfInheritance(JsonSchema schema, JsonSchema baseSchema, string baseTypeNameHint)
            {
                if (IsExternal(baseSchema))
                {
                    foreach (var derivedSchema in schema.OneOf)
                    {
                        if (derivedSchema.IsNullable(SchemaType.JsonSchema))
                            continue;

                        var actualSchema = derivedSchema.ActualSchema;
                        if (!actualSchema.AllOf.Any(schema => schema.Reference == baseSchema))
                        {
                            actualSchema.AllOf.Add(new JsonSchema { Reference = baseSchema });
                        }
                    }
                    return;
                }

                var baseTypeName = Settings.TypeNameGenerator.Generate(baseSchema, baseTypeNameHint, Array.Empty<string>());
                foreach (var derivedSchema in schema.OneOf.ToList())
                {
                    if (derivedSchema.IsNullable(SchemaType.JsonSchema))
                        continue;

                    var memberSchema = derivedSchema.ActualSchema;
                    if (memberSchema.AllOf.Any(schema => schema.Reference == baseSchema) ||
                        memberSchema.TryGetUnionWrapperTag(out _))
                        continue;

                    if (!IsExternal(memberSchema) && !InheritsOtherType(memberSchema, baseSchema))
                    {
                        memberSchema.AllOf.Add(new JsonSchema { Reference = baseSchema });
                        continue;
                    }

                    if (memberSchema.DiscriminatorObject != null)
                    {
                        definitionTypeNameLookup.TryGetValue(memberSchema, out var memberTypeName);
                        throw new InvalidOperationException(
                            $"The discriminated union '{baseTypeName}' has a member '{memberTypeName}' which is " +
                            "itself a discriminated union. Declare a single union listing every member directly.");
                    }

                    var wrapperSchema = CreateUnionWrapper(baseSchema, baseTypeName, memberSchema);
                    ReplaceUnionMember(schema.OneOf, derivedSchema, wrapperSchema);
                    if (baseSchema.DiscriminatorObject is OpenApiDiscriminator discriminator)
                    {
                        foreach (var mapping in discriminator.Mapping.ToList())
                        {
                            if (mapping.Value.ActualSchema == memberSchema)
                                discriminator.Mapping[mapping.Key] = new JsonSchema { Reference = wrapperSchema };
                        }
                    }
                }
            }

            private JsonSchema CreateUnionWrapper(JsonSchema baseSchema, string baseTypeName, JsonSchema memberSchema)
            {
                var tag = baseSchema.DiscriminatorObject?.Mapping
                    .FirstOrDefault(mapping => mapping.Value.ActualSchema == memberSchema).Key;
                if (string.IsNullOrEmpty(tag))
                {
                    throw new InvalidOperationException(
                        $"The discriminated union '{baseTypeName}' has a member with no discriminator mapping.");
                }

                var wrapperTypeName = baseTypeName + ToIdentifier(tag);
                var clashesWithDefinition = RootObject.Definitions.Any(definition =>
                    definition.Key == wrapperTypeName ||
                    definition.Value.TryGetExternalTypeName(out var typeName) &&
                    CSharpTypeNameGenerator.NamespaceEquals(typeName, Settings.Namespace) &&
                    CSharpTypeNameGenerator.GetTypeNameWithoutNamespace(typeName) == wrapperTypeName);
                if (clashesWithDefinition)
                {
                    throw new InvalidOperationException(
                        $"The wrapper type '{wrapperTypeName}' for the member '{tag}' of the discriminated union " +
                        $"'{baseTypeName}' clashes with an existing type. Rename the discriminator value to resolve it.");
                }

                var discriminatorName = baseSchema.DiscriminatorObject!.PropertyName;
                if (memberSchema.ActualProperties.TryGetValue(discriminatorName, out var discriminatorProperty) &&
                    !IsConstant(discriminatorProperty))
                {
                    throw new InvalidOperationException(
                        $"The member '{tag}' of the discriminated union '{baseTypeName}' has a property " +
                        $"'{discriminatorName}' that clashes with the discriminator. Declare the property as a " +
                        "constant or rename it to resolve it.");
                }

                var wrapperSchema = new JsonSchema { Type = JsonObjectType.Object };
                wrapperSchema.AllOf.Add(new JsonSchema { Reference = baseSchema });
                wrapperSchema.Properties.Add(UnionWrapperValueProperty, new JsonSchemaProperty { Reference = memberSchema });
                wrapperSchema.ExtensionData = new Dictionary<string, object>
                {
                    [TypeNameAnnotation] = $"{Settings.Namespace}.{wrapperTypeName}",
                    [UnionWrapperTag] = new DiscriminatorTag(tag)
                };
                RootObject.Definitions.Add(wrapperTypeName, wrapperSchema);
                return wrapperSchema;
            }

            private static bool IsConstant(JsonSchema schema)
            {
                var actualSchema = schema.ActualSchema;
                return actualSchema.ExtensionData?.ContainsKey(ConstKeyword) is true || actualSchema.Enumeration.Count == 1;
            }

            private static void ReplaceUnionMember(ICollection<JsonSchema> members, JsonSchema member, JsonSchema wrapperSchema)
            {
                if (member.HasReference)
                {
                    member.Reference = wrapperSchema;
                    return;
                }

                var index = members.ToList().IndexOf(member);
                var replacement = members.ToList();
                replacement[index] = new JsonSchema { Reference = wrapperSchema };
                members.Clear();
                foreach (var schema in replacement)
                {
                    members.Add(schema);
                }
            }

            private static string ToIdentifier(string value)
            {
                var parts = System.Text.RegularExpressions.Regex.Split(value, "[^A-Za-z0-9]+")
                    .Where(part => part.Length > 0)
                    .Select(part => char.ToUpperInvariant(part[0]) + part[1..]);
                var identifier = string.Concat(parts);
                return identifier.Length == 0 || char.IsDigit(identifier[0]) ? "_" + identifier : identifier;
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
                            if (actualSchema.ExtensionData?.Count > 0)
                            {
                                discriminatorSchema.ExtensionData = new Dictionary<string, object>(actualSchema.ExtensionData);
                            }
                            RootObject.Definitions.Add(typeNameHint, discriminatorSchema);
                            ResolveOneOfInheritance(actualSchema, discriminatorSchema, typeNameHint);
                        }
                        else
                        {
                            if (discriminatorSchema.OneOf.Count > 0)
                            {
                                var baseTypeNameHint = definitionTypeNameLookup.TryGetValue(discriminatorSchema, out var definitionName)
                                    ? definitionName
                                    : typeNameHint;
                                ResolveOneOfInheritance(discriminatorSchema, discriminatorSchema, baseTypeNameHint);
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

        class AnyOfNullablePropertySchemaVisitor : JsonSchemaVisitorBase
        {
            protected override JsonSchema VisitSchema(JsonSchema schema, string path, string typeNameHint)
            {
                if (schema.OneOf.Count == 0 && schema.AnyOf.Count == 2)
                {
                    if (schema.AnyOf.Count(anyOf => anyOf.IsNullable(SchemaType.JsonSchema)) == 1)
                    {
                        foreach (var anyOf in schema.AnyOf)
                        {
                            schema.OneOf.Add(anyOf);
                        }
                        schema.AnyOf.Clear();
                    }
                }

                return schema;
            }
        }

        class DefinitionSchemaVisitor : JsonSchemaVisitorBase
        {
            const string DefsExtension = "$defs";
            const string ReferenceKeyword = "$ref";
            const string PropertyNamesExtension = "propertyNames";

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

            public static void ResolveDefinitionSchemas(JsonSchema schema, IContractResolver contractResolver, string path)
            {
                if (schema.ExtensionData?.TryGetValue(DefsExtension, out var defs) is true &&
                    defs is IDictionary<string, object> definitions)
                {
                    foreach (var entry in definitions.ToList())
                    {
                        JsonSchema definition;
                        if (entry.Value is IDictionary dictionary)
                        {
                            var settings = new JsonSerializerSettings { ContractResolver = contractResolver };
                            var json = JsonConvert.SerializeObject(dictionary, settings);
                            definition = JsonConvert.DeserializeObject<JsonSchema>(json, settings) ??
                                throw new InvalidOperationException(
                                    $"Unable to resolve definition {entry.Key} within JSON path '{path}'.");
                            definitions[entry.Key] = definition;
                        }
                        else definition = (JsonSchema)entry.Value;

                        if (schema.Definitions.TryGetValue(entry.Key, out var existing))
                        {
                            if (existing != definition)
                                throw new InvalidOperationException(
                                    $"The key '{entry.Key}' in '$defs' conflicts with an existing definition.");
                            else continue;
                        }
                        schema.Definitions.Add(entry.Key, definition);
                    }
                }
            }

            protected override JsonSchema VisitSchema(JsonSchema schema, string path, string typeNameHint)
            {
                ResolveDefinitionSchemas(schema, ContractResolver, path);

                if (schema.IsDictionary &&
                    schema.ExtensionData?.TryGetValue(PropertyNamesExtension, out var value) is true &&
                    value is IDictionary<string, object> propertyNames &&
                    propertyNames.TryGetValue(ReferenceKeyword, out var referenceValue) &&
                    referenceValue is string referencePath)
                {
                    var reference = ReferenceResolver.ResolveReferenceAsync(
                        RootObject,
                        referencePath,
                        typeof(JsonSchema),
                        ContractResolver).Result;
                    if (reference is JsonSchema propertyNamesSchema)
                    {
                        schema.ExtensionData[PropertyNamesSchema] = propertyNamesSchema;
                    }
                }

                return schema;
            }
        }
    }
}
