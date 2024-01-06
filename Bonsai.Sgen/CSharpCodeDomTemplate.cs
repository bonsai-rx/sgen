using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;
using NJsonSchema.CodeGeneration;

namespace Bonsai.Sgen
{
    internal abstract class CSharpCodeDomTemplate : ITemplate
    {
        private static readonly AssemblyName GeneratorAssemblyName = Assembly.GetExecutingAssembly().GetName();

        public CSharpCodeDomTemplate(
            CodeDomProvider provider,
            CodeGeneratorOptions options,
            CSharpCodeDomGeneratorSettings settings)
        {
            Provider = provider;
            Options = options;
            Settings = settings;
        }

        public CodeDomProvider Provider { get; }

        public CodeGeneratorOptions Options { get; }

        public CSharpCodeDomGeneratorSettings Settings { get; }

        public abstract string TypeName { get; }

        public abstract void BuildType(CodeTypeDeclaration type);

        public string Render()
        {
            var type = new CodeTypeDeclaration(TypeName) { IsPartial = true };
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference(typeof(GeneratedCodeAttribute)),
                new CodeAttributeArgument(new CodePrimitiveExpression(GeneratorAssemblyName.Name)),
                new CodeAttributeArgument(new CodePrimitiveExpression(GeneratorAssemblyName.Version?.ToString()))));
            BuildType(type);

            using var writer = new StringWriter();
            Provider.GenerateCodeFromType(type, writer, Options);
            return writer.ToString();
        }
    }
}
