using System.CodeDom;
using System.CodeDom.Compiler;
using NJsonSchema;

namespace Bonsai.Sgen
{
    internal class CSharpPythonDeserializerTemplate : CSharpDeserializerTemplate
    {
        public CSharpPythonDeserializerTemplate(
            JsonSchema schema,
            IEnumerable<CSharpClassCodeArtifact> modelTypes,
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
            : base(schema, modelTypes, provider, options, settings)
        {
        }

        public override string Description => "Converts a sequence of generic Python objects into data model objects.";

        public override string TypeName => "FromPython";

        public override void BuildType(CodeTypeDeclaration type)
        {
            base.BuildType(type);
            type.Members.Add(new CodeSnippetTypeMember(
@"    private static System.IObservable<T> Process<T>(System.IObservable<Python.Runtime.PyObject> source)
    {
        return System.Reactive.Linq.Observable.Select(source, value => value.As<T>());
    }"));
        }
    }
}
