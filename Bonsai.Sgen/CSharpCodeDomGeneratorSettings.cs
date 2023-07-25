using NJsonSchema.CodeGeneration.CSharp;

namespace Bonsai.Sgen
{
    internal class CSharpCodeDomGeneratorSettings : CSharpGeneratorSettings
    {
        public SerializerLibraries SerializerLibraries { get; set; }
    }
}
