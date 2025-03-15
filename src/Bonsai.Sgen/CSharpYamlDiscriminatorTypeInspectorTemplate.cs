using System.CodeDom;
using System.CodeDom.Compiler;
using YamlDotNet.Serialization.TypeInspectors;

namespace Bonsai.Sgen
{
    internal class CSharpYamlDiscriminatorTypeInspectorTemplate : CSharpCodeDomTemplate
    {
        public CSharpYamlDiscriminatorTypeInspectorTemplate(
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(provider, options, settings)
        {
        }

        public override string TypeName => "YamlDiscriminatorTypeInspector";

        public override void BuildType(CodeTypeDeclaration type)
        {
            type.IsPartial = false;
            type.BaseTypes.Add(typeof(TypeInspectorSkeleton));
            type.Members.Add(new CodeSnippetTypeMember(
@$"    readonly YamlDotNet.Serialization.ITypeInspector innerTypeDescriptor;

    public {TypeName}(YamlDotNet.Serialization.ITypeInspector innerTypeDescriptor)
    {{
        if (innerTypeDescriptor == null)
        {{
            throw new System.ArgumentNullException(""innerTypeDescriptor"");
        }}

        this.innerTypeDescriptor = innerTypeDescriptor;
    }}

    public override System.Collections.Generic.IEnumerable<YamlDotNet.Serialization.IPropertyDescriptor> GetProperties(System.Type type, object container)
    {{
        var innerProperties = innerTypeDescriptor.GetProperties(type, container);

        var discriminatorAttribute = (YamlDiscriminatorAttribute)System.Attribute.GetCustomAttribute(type, typeof(YamlDiscriminatorAttribute));
        var inheritanceAttributes = (JsonInheritanceAttribute[])System.Attribute.GetCustomAttributes(type, typeof(JsonInheritanceAttribute));
        var typeMatch = System.Array.Find(inheritanceAttributes, attribute => attribute.Type == type);
        if (discriminatorAttribute != null && typeMatch != null)
        {{
            return System.Linq.Enumerable.Concat(new[]
            {{
                new DiscriminatorPropertyDescriptor(discriminatorAttribute.Discriminator, typeMatch.Key)
            }}, innerProperties);
        }}

        return innerProperties;
    }}

    class DiscriminatorPropertyDescriptor : YamlDotNet.Serialization.IPropertyDescriptor
    {{
        readonly string key;

        public DiscriminatorPropertyDescriptor(string discriminator, string value)
        {{
            ScalarStyle = YamlDotNet.Core.ScalarStyle.Plain;
            Name = discriminator;
            key = value;
        }}

        public string Name {{ get; private set; }}

        public bool CanWrite
        {{
            get {{ return true; }}
        }}

        public System.Type Type
        {{
            get {{ return typeof(string); }}
        }}

        public System.Type TypeOverride {{ get; set; }}

        public int Order {{ get; set; }}

        public YamlDotNet.Core.ScalarStyle ScalarStyle {{ get; set; }}

        public T GetCustomAttribute<T>() where T : System.Attribute
        {{
            return null;
        }}

        public YamlDotNet.Serialization.IObjectDescriptor Read(object target)
        {{
            return new YamlDotNet.Serialization.ObjectDescriptor(key, Type, Type, ScalarStyle);
        }}

        public void Write(object target, object value)
        {{
        }}
    }}"));
        }
    }
}
