using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;
using NJsonSchema.CodeGeneration;

namespace Bonsai.Sgen
{
    internal abstract class CSharpCodeDomTemplate : ITemplate
    {
        private static readonly AssemblyName GeneratorAssemblyName = Assembly.GetExecutingAssembly().GetName();
        private static readonly AssemblyName NewtonsoftJsonAssemblyName = typeof(Newtonsoft.Json.JsonConvert).Assembly.GetName();
        private static readonly AssemblyName YamlDotNetAssemblyName = typeof(YamlDotNet.Core.Parser).Assembly.GetName();

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

        private static string GetVersionString(AssemblyName assemblyName)
        {
            return $"{assemblyName.Name} v{assemblyName.Version}";
        }

        private string GetVersionString()
        {
            var serializerLibraries = new List<string>();
            if (Settings.SerializerLibraries.HasFlag(SerializerLibraries.NewtonsoftJson))
            {
                serializerLibraries.Add(GetVersionString(NewtonsoftJsonAssemblyName));
            }
            if (Settings.SerializerLibraries.HasFlag(SerializerLibraries.YamlDotNet))
            {
                serializerLibraries.Add(GetVersionString(YamlDotNetAssemblyName));
            }
            return $"{GeneratorAssemblyName.Version} ({string.Join(", ", serializerLibraries)})";
        }

        public string Render()
        {
            var type = new CodeTypeDeclaration(TypeName) { IsPartial = true };
            type.CustomAttributes.Add(new CodeAttributeDeclaration(
                new CodeTypeReference(typeof(GeneratedCodeAttribute)),
                new CodeAttributeArgument(new CodePrimitiveExpression(GeneratorAssemblyName.Name)),
                new CodeAttributeArgument(new CodePrimitiveExpression(GetVersionString()))));
            BuildType(type);

            using var writer = new StringWriter();
            Provider.GenerateCodeFromType(type, writer, Options);
            return writer.ToString();
        }

        protected static readonly Dictionary<string, string> PrimitiveTypes = new()
        {
            { "bool",    "System.Boolean" },
            { "byte",    "System.Byte" },
            { "sbyte",   "System.SByte" },
            { "char",    "System.Char" },
            { "decimal", "System.Decimal" },
            { "double",  "System.Double" },
            { "float",   "System.Single" },
            { "int",     "System.Int32" },
            { "uint",    "System.UInt32" },
            { "nint",    "System.IntPtr" },
            { "nuint",   "System.UIntPtr" },
            { "long",    "System.Int64" },
            { "ulong",   "System.UInt64" },
            { "short",   "System.Int16" },
            { "ushort",  "System.UInt16" },

            { "object",  "System.Object" },
            { "string",  "System.String" }
        };
    }
}
