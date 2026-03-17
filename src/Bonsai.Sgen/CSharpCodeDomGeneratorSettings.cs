using NJsonSchema.CodeGeneration.CSharp;

namespace Bonsai.Sgen
{
    internal class CSharpCodeDomGeneratorSettings : CSharpGeneratorSettings
    {
        public CSharpCodeDomGeneratorSettings()
        {
            GenerateDataAnnotations = false;
            GenerateJsonMethods = true;
            TypeNameGenerator = new CSharpTypeNameGenerator(this);
            EnumNameGenerator = new CSharpEnumNameGenerator();
            PropertyNameGenerator = new CSharpPropertyNameGenerator();
            ValueGenerator = new CSharpValueGenerator(this);
            JsonLibrary = CSharpJsonLibrary.NewtonsoftJson;
            ArrayInstanceType = "System.Collections.Generic.List";
            ArrayBaseType = "System.Collections.Generic.List";
            ArrayType = "System.Collections.Generic.List";
            DictionaryInstanceType = "System.Collections.Generic.Dictionary";
            DictionaryBaseType = "System.Collections.Generic.Dictionary";
            DictionaryType = "System.Collections.Generic.Dictionary";
            SetInstanceType = "System.Collections.Generic.HashSet";
            SetBaseType = "System.Collections.Generic.HashSet";
            SetType = "System.Collections.Generic.HashSet";
        }

        public string SetInstanceType { get; set; }

        public string SetBaseType { get; set; }

        public string SetType { get; set; }

        public SerializerLibraries SerializerLibraries { get; set; }
    }
}
