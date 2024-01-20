using NJsonSchema;
using NJsonSchema.CodeGeneration;
using NJsonSchema.Visitors;

namespace Bonsai.Sgen
{
    internal static class JsonSchemaExtensions
    {
        public static JsonSchema WithUniqueDiscriminatorProperties(this JsonSchema schema)
        {
            var visitor = new DiscriminatorSchemaVisitor(schema);
            visitor.Visit(schema);
            return schema;
        }

        class DiscriminatorSchemaVisitor : JsonSchemaVisitorBase
        {
            public DiscriminatorSchemaVisitor(JsonSchema rootObject)
            {
                RootObject = rootObject;
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

                    derivedSchema.ActualSchema.AllOf.Add(new JsonSchema { Reference = baseSchema });
                }
            }

            protected override JsonSchema VisitSchema(JsonSchema schema, string path, string typeNameHint)
            {
                var actualSchema = schema.ActualSchema;
                if (actualSchema.DiscriminatorObject != null)
                {
                    if (schema is JsonSchemaProperty || schema.ParentSchema?.Item == schema)
                    {
                        if (string.IsNullOrEmpty(typeNameHint))
                        {
                            typeNameHint = "Anonymous";
                        }

                        if (!RootObject.Definitions.TryGetValue(typeNameHint, out JsonSchema? discriminatorSchema))
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
                                discriminatorSchema.OneOf.Clear();
                            }
                        }

                        schema.DiscriminatorObject = null;
                        schema.IsAbstract = false;
                        return schema;
                    }

                    foreach (var derivedSchema in schema.GetDerivedSchemas(RootObject).Keys)
                    {
                        foreach (var property in derivedSchema.Properties.Keys.ToList())
                        {
                            if (property == schema.Discriminator)
                            {
                                derivedSchema.Properties.Remove(property);
                            }
                        }
                    }
                }

                return schema;
            }
        }
    }
}
