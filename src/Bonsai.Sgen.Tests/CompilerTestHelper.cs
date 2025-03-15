using Basic.Reference.Assemblies;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bonsai.Sgen.Tests
{
    internal static class CompilerTestHelper
    {
        public static void CompileFromSource(params string[] code)
        {
            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp5);
            var syntaxTrees = Array.ConvertAll(code, text => CSharpSyntaxTree.ParseText(text, options));
            var serializerDependencies = new[]
            {
                typeof(YamlDotNet.Core.Parser).Assembly.Location,
                typeof(Newtonsoft.Json.JsonConvert).Assembly.Location,
                typeof(System.Reactive.Linq.Observable).Assembly.Location,
                typeof(Combinator).Assembly.Location
            };
            var assemblyReferences = serializerDependencies.Select(path => MetadataReference.CreateFromFile(path)).ToList();
            assemblyReferences.AddRange(Net80.References.All);

            var compilation = CSharpCompilation.Create(
                nameof(CompilerTestHelper),
                syntaxTrees: syntaxTrees,
                references: assemblyReferences,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using var memoryStream = new MemoryStream();
            var result = compilation.Emit(memoryStream);
            if (!result.Success)
            {
                var errorMessages = (from diagnostic in result.Diagnostics
                                     where diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error
                                     select $"{diagnostic.Id}: {diagnostic.GetMessage()}")
                                     .ToList();
                Assert.Fail(string.Join(Environment.NewLine, errorMessages));
            }
        }
    }
}
