using NJsonSchema;
using NJsonSchema.CodeGeneration;
using NJsonSchema.CodeGeneration.CSharp;

namespace Bonsai.Sgen
{
    public class CSharpValueGenerator(CSharpGeneratorSettings settings) : NJsonSchema.CodeGeneration.CSharp.CSharpValueGenerator(settings)
    {
        readonly EnumValueGeneratorBase enumValueGenerator = new(settings);

        protected override string GetEnumDefaultValue(JsonSchema schema, JsonSchema actualSchema, string typeNameHint, TypeResolverBase typeResolver)
        {
            return enumValueGenerator.GetEnumDefaultValue(schema, actualSchema, typeNameHint, typeResolver);
        }

        class EnumValueGeneratorBase(CodeGeneratorSettingsBase settings) : ValueGeneratorBase(settings)
        {
            public new string GetEnumDefaultValue(JsonSchema schema, JsonSchema actualSchema, string typeNameHint, TypeResolverBase typeResolver)
            {
                return base.GetEnumDefaultValue(schema, actualSchema, typeNameHint, typeResolver);
            }

            public override string GetNumericValue(JsonObjectType type, object value, string format)
            {
                throw new NotSupportedException();
            }
        }
    }
}
