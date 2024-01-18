using NJsonSchema.CodeGeneration.CSharp;

namespace Bonsai.Sgen
{
    internal class CSharpCodeDomGeneratorSettings : CSharpGeneratorSettings
    {
        public CSharpCodeDomGeneratorSettings()
        {
            GenerateDataAnnotations = false;
            GenerateJsonMethods = true;
            TypeNameGenerator = new CSharpTypeNameGenerator();
            EnumNameGenerator = new CSharpEnumNameGenerator();
            PropertyNameGenerator = new CSharpPropertyNameGenerator();
            JsonLibrary = CSharpJsonLibrary.NewtonsoftJson;
            ArrayInstanceType = "System.Collections.Generic.List";
            ArrayBaseType = "System.Collections.Generic.List";
            ArrayType = "System.Collections.Generic.List";
        }

        public SerializerLibraries SerializerLibraries { get; set; }
    }
}
