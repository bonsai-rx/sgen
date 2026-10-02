using System.CodeDom;
using System.CodeDom.Compiler;
using YamlDotNet.Serialization.TypeInspectors;

namespace Bonsai.Sgen
{
    internal class CSharpYamlDiscriminatorTypeInspectorTemplate : CSharpCodeDomTemplate
    {
        public CSharpYamlDiscriminatorTypeInspectorTemplate(
            bool hasUnionWrappers,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(provider, options, settings)
        {
            HasUnionWrappers = hasUnionWrappers;
        }

        public bool HasUnionWrappers { get; }

        public override string TypeName => "YamlDiscriminatorTypeInspector";

        public override void BuildType(CodeTypeDeclaration type)
        {
            type.IsPartial = false;
            type.BaseTypes.Add(typeof(ReflectionTypeInspector));
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
{(HasUnionWrappers ? UnionWrapperPropertiesSnippet : string.Empty)}            return System.Linq.Enumerable.Concat(new[]
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

        public bool Required
        {{
            get {{ return true; }}
        }}

        public bool CanWrite
        {{
            get {{ return true; }}
        }}

        public System.Type Type
        {{
            get {{ return typeof(string); }}
        }}

        public System.Type TypeOverride {{ get; set; }}

        public System.Type ConverterType
        {{
            get {{ return null; }}
        }}

        public bool AllowNulls
        {{
            get {{ return false; }}
        }}

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
            if (HasUnionWrappers)
            {
                type.Members.Add(new CodeSnippetTypeMember(UnionValuePropertyDescriptorSnippet));
            }
        }

        const string UnionWrapperPropertiesSnippet =
@$"            var unionWrapperAttribute = ({CSharpYamlUnionWrapperTemplate.AttributeName}Attribute)System.Attribute.GetCustomAttribute(type, typeof({CSharpYamlUnionWrapperTemplate.AttributeName}Attribute));
            if (unionWrapperAttribute != null)
            {{
                var valueType = unionWrapperAttribute.ValueType;
                var value = container != null ? (({CSharpUnionWrapperInterfaceTemplate.InterfaceName})container).{JsonSchemaExtensions.UnionWrapperValueProperty} : null;
                innerProperties = System.Linq.Enumerable.Select(
                    System.Linq.Enumerable.Where(
                        innerTypeDescriptor.GetProperties(valueType, value),
                        descriptor => descriptor.Name != discriminatorAttribute.Discriminator),
                    descriptor => (YamlDotNet.Serialization.IPropertyDescriptor)new UnionValuePropertyDescriptor(valueType, descriptor));
            }}

";

        const string UnionValuePropertyDescriptorSnippet =
@"
    class UnionValuePropertyDescriptor : YamlDotNet.Serialization.IPropertyDescriptor
    {
        readonly System.Type valueType;
        readonly YamlDotNet.Serialization.IPropertyDescriptor innerDescriptor;

        public UnionValuePropertyDescriptor(System.Type valueType, YamlDotNet.Serialization.IPropertyDescriptor innerDescriptor)
        {
            this.valueType = valueType;
            this.innerDescriptor = innerDescriptor;
        }

        public string Name
        {
            get { return innerDescriptor.Name; }
        }

        public bool Required
        {
            get { return innerDescriptor.Required; }
        }

        public bool CanWrite
        {
            get { return innerDescriptor.CanWrite; }
        }

        public System.Type Type
        {
            get { return innerDescriptor.Type; }
        }

        public System.Type TypeOverride
        {
            get { return innerDescriptor.TypeOverride; }
            set { innerDescriptor.TypeOverride = value; }
        }

        public System.Type ConverterType
        {
            get { return innerDescriptor.ConverterType; }
        }

        public bool AllowNulls
        {
            get { return innerDescriptor.AllowNulls; }
        }

        public int Order
        {
            get { return innerDescriptor.Order; }
            set { innerDescriptor.Order = value; }
        }

        public YamlDotNet.Core.ScalarStyle ScalarStyle
        {
            get { return innerDescriptor.ScalarStyle; }
            set { innerDescriptor.ScalarStyle = value; }
        }

        public T GetCustomAttribute<T>() where T : System.Attribute
        {
            return innerDescriptor.GetCustomAttribute<T>();
        }

        public YamlDotNet.Serialization.IObjectDescriptor Read(object target)
        {
            return innerDescriptor.Read(((" + CSharpUnionWrapperInterfaceTemplate.InterfaceName + @")target)." + JsonSchemaExtensions.UnionWrapperValueProperty + @");
        }

        public void Write(object target, object value)
        {
            var wrapper = (" + CSharpUnionWrapperInterfaceTemplate.InterfaceName + @")target;
            var member = wrapper." + JsonSchemaExtensions.UnionWrapperValueProperty + @";
            if (member == null)
            {
                member = System.Activator.CreateInstance(valueType);
            }
            innerDescriptor.Write(member, value);
            wrapper." + JsonSchemaExtensions.UnionWrapperValueProperty + @" = member;
        }
    }";
    }
}
