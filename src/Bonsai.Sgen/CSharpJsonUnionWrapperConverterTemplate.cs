using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;
using Newtonsoft.Json;

namespace Bonsai.Sgen
{
    internal class CSharpJsonUnionWrapperConverterTemplate : CSharpCodeDomTemplate
    {
        public const string ConverterName = "JsonUnionWrapperConverter";

        public CSharpJsonUnionWrapperConverterTemplate(
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(provider, options, settings)
        {
        }

        public override string TypeName => ConverterName;

        public override void BuildType(CodeTypeDeclaration type)
        {
            type.IsPartial = false;
            type.TypeAttributes = (type.TypeAttributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.NotPublic;
            type.BaseTypes.Add(typeof(JsonConverter));
            type.Members.Add(new CodeSnippetTypeMember(
@$"    readonly System.Type valueType;
    readonly string discriminatorName;
    readonly string discriminatorValue;

    public {TypeName}(System.Type valueType, string discriminatorName, string discriminatorValue)
    {{
        this.valueType = valueType;
        this.discriminatorName = discriminatorName;
        this.discriminatorValue = discriminatorValue;
    }}

    public override bool CanConvert(System.Type objectType)
    {{
        return true;
    }}

    public override void WriteJson(Newtonsoft.Json.JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
    {{
        var wrapper = ({CSharpUnionWrapperInterfaceTemplate.InterfaceName})value;
        var jObject = Newtonsoft.Json.Linq.JObject.FromObject(wrapper.{JsonSchemaExtensions.UnionWrapperValueProperty}, serializer);
        jObject.Remove(discriminatorName);
        jObject.AddFirst(new Newtonsoft.Json.Linq.JProperty(discriminatorName, discriminatorValue));
        jObject.WriteTo(writer);
    }}

    public override object ReadJson(Newtonsoft.Json.JsonReader reader, System.Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
    {{
        var jObject = Newtonsoft.Json.Linq.JObject.Load(reader);
        jObject[discriminatorName] = discriminatorValue;
        var wrapper = ({CSharpUnionWrapperInterfaceTemplate.InterfaceName})System.Activator.CreateInstance(objectType, true);
        wrapper.{JsonSchemaExtensions.UnionWrapperValueProperty} = jObject.ToObject(valueType, serializer);
        return wrapper;
    }}"));
        }
    }
}
