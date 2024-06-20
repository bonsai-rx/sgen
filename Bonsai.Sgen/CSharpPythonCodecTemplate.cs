using System.CodeDom;
using System.CodeDom.Compiler;
using NJsonSchema.CodeGeneration;

namespace Bonsai.Sgen
{
    internal class CSharpPythonCodecTemplate : CSharpSerializerTemplate
    {
        const string DataVariableName = "value";
        static readonly CodeTypeReference PyObjectTypeReference = new("Python.Runtime.PyObject");

        public CSharpPythonCodecTemplate(
            IEnumerable<CodeArtifact> modelTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(modelTypes, provider, options, settings)
        {
        }

        public override string Description => "Provides a collection of methods for converting generic Python objects into data model objects.";

        public override string TypeName => "PyObjectConverter";

        public override void BuildType(CodeTypeDeclaration type)
        {
            base.BuildType(type);
            type.BaseTypes.Add("Bonsai.Sink");
            type.BaseTypes.Add("Python.Runtime.IPyObjectDecoder");

            var decoderItems = new List<string>();
            var decoderDictionaryMember = new CodeSnippetTypeMember();
            type.Members.Add(decoderDictionaryMember);

            type.Members.Add(new CodeSnippetTypeMember(
@"    private static T GetAttr<T>(Python.Runtime.PyObject pyObj, string attributeName)
    {
        using (var attr = pyObj.GetAttr(attributeName))
        {
            return attr.As<T>();
        }
    }

    static System.Func<Python.Runtime.PyObject, object> CreateDecoder<T>(System.Action<Python.Runtime.PyObject, T> decode)
        where T : new()
    {
        return pyObj =>
        {
            T value = new T();
            decode(pyObj, value);
            return value;
        };
    }

    public bool CanDecode(Python.Runtime.PyType objectType, System.Type targetType)
    {
        return decoders.ContainsKey(targetType);
    }

    public bool TryDecode<T>(Python.Runtime.PyObject pyObj, out T value)
    {
        System.Func<Python.Runtime.PyObject, object> decoder;
        if (decoders.TryGetValue(typeof(T), out decoder))
        {
            value = (T)decoder(pyObj);
            return true;
        }
        value = default(T);
        return false;
    }"));

            foreach (var modelType in ModelTypes.Cast<CSharpClassCodeArtifact>())
            {
                var model = modelType.Model;
                var modelTypeReference = new CodeTypeReference(model.ClassName);
                var decodeMethod = new CodeMemberMethod
                {
                    Name = $"Decode{model.ClassName}",
                    Attributes = MemberAttributes.Static,
                    Parameters = { new(PyObjectTypeReference, "pyObj"), new(modelTypeReference, DataVariableName) },
                };

                var modelObjectReference = new CodeVariableReferenceExpression(DataVariableName);
                var pyObjectReference = new CodeVariableReferenceExpression("pyObj");
                if (model.BaseClass != null)
                {
                    decodeMethod.Statements.Add(new CodeMethodInvokeExpression(
                        targetObject: null,
                        $"Decode{model.BaseClassName}",
                        pyObjectReference,
                        modelObjectReference));
                }

                foreach (var property in model.Properties)
                {
                    var isPrimitive = PrimitiveTypes.TryGetValue(property.Type, out string? underlyingType);
                    var propertyTypeReference = new CodeTypeReference(isPrimitive ? underlyingType : property.Type);
                    decodeMethod.Statements.Add(new CodeAssignStatement(
                        new CodePropertyReferenceExpression(modelObjectReference, property.PropertyName),
                        new CodeMethodInvokeExpression(
                            new CodeMethodReferenceExpression(null, "GetAttr", propertyTypeReference),
                            pyObjectReference,
                            new CodePrimitiveExpression(property.Name))));
                }
                type.Members.Add(decodeMethod);
                if (!model.IsAbstract)
                {
                    decoderItems.Add($"            {{ typeof({model.ClassName}), CreateDecoder<{model.ClassName}>({decodeMethod.Name}) }}");
                }
            }

            decoderDictionaryMember.Text =
@$"    static readonly System.Collections.Generic.Dictionary<System.Type, System.Func<Python.Runtime.PyObject, object>> decoders =
        new System.Collections.Generic.Dictionary<System.Type, System.Func<Python.Runtime.PyObject, object>>
        {{
{string.Join($",{Environment.NewLine}", decoderItems)}
        }};
";

            type.Members.Add(new CodeSnippetTypeMember(
@"    public override System.IObservable<TSource> Process<TSource>(System.IObservable<TSource> source)
    {
        return System.Reactive.Linq.Observable.Do(source, _ => Python.Runtime.PyObjectConversions.RegisterDecoder(this));
    }"));
        }
    }
}
