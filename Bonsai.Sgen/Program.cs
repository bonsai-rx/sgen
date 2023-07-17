using System.CommandLine;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;

namespace Bonsai.Sgen
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var schemaPath = new Option<string>(
                name: "--schema",
                getDefaultValue: () => "schema.json",
                description: "Generates YML serialization classes for data types in the specified schema file.");
            var generatorNamespace = new Option<string?>(
                name: "--namespace",
                getDefaultValue: () => "DataSchema",
                description: "Specifies the namespace to use for all generated serialization classes.");
            var outputPath = new Option<string>(
                name: "--output",
                getDefaultValue: () => "GeneratedClasses.cs",
                description: "Specifies the name of the file containing the generated code.");
            
            var rootCommand = new RootCommand("Tool for automatically generating YML serialization classes from schema files.");
            rootCommand.AddOption(schemaPath);
            rootCommand.AddOption(generatorNamespace);
            rootCommand.AddOption(outputPath);
            rootCommand.SetHandler(async (filePath, generatorNamespace, outputFilePath) =>
            {
                var schema = await JsonSchema.FromFileAsync(filePath);
                var settings = new CSharpGeneratorSettings
                {
                    Namespace = generatorNamespace,
                    GenerateDataAnnotations = false,
                    GenerateJsonMethods = false,
                    JsonLibrary = CSharpJsonLibrary.NewtonsoftJson,
                    ArrayBaseType = "System.Collections.Generic.IList",
                    ArrayType = "System.Collections.Generic.IList"
                };

                var generator = new CSharpGenerator(schema, settings);
                var code = generator.GenerateFile();
                File.WriteAllText(outputFilePath, code);
            }, schemaPath, generatorNamespace, outputPath);
            await rootCommand.InvokeAsync(args);
        }
    }
}
