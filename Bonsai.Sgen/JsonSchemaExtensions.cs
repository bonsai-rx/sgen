using NJsonSchema;
using NJsonSchema.CodeGeneration;
using NJsonSchema.Visitors;

namespace Bonsai.Sgen
{
    public static class JsonSchemaExtensions
    {
        public static JsonSchema WithUniqueDiscriminatorProperties(this JsonSchema schema)
        {
            var visitor = new DiscriminatorSchemaVisitor(schema);
            visitor.Visit(schema);
            return schema;
        }

        class DiscriminatorSchemaVisitor : JsonSchemaVisitorBase
        {
            public DiscriminatorSchemaVisitor(object rootObject)
            {
                RootObject = rootObject;
            }

            public object RootObject { get; }

            protected override JsonSchema VisitSchema(JsonSchema schema, string path, string typeNameHint)
            {
                if (schema.DiscriminatorObject != null)
                {
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
