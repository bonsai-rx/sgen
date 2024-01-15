using System.CommandLine;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;

namespace Bonsai.Sgen
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var schemaPath = new Option<FileInfo>(
                name: "--schema",
                description: "Generates serialization classes for data types in the specified schema file.")
                { IsRequired = true };
            var generatorNamespace = new Option<string?>(
                name: "--namespace",
                getDefaultValue: () => "DataSchema",
                description: "Specifies the namespace to use for all generated serialization classes.");
            var generatorTypeName = new Option<string?>(
                name: "--root",
                description: "Specifies the name of the class used to represent the schema root element.");
            var outputPath = new Option<string>(
                name: "--output",
                description: "Specifies the name of the file containing the generated code.");
            var serializerLibraries = new Option<SerializerLibraries>(
                name: "--serializer",
                description: "Specifies the serializer data annotations to include in the generated classes.",
                parseArgument: result =>
                {
                    SerializerLibraries serializers = default;
                    foreach (var token in result.Tokens)
                    {
                        serializers |= (SerializerLibraries)Enum.Parse(typeof(SerializerLibraries), token.Value);
                    }
                    return serializers;
                }) { AllowMultipleArgumentsPerToken = true, Arity = ArgumentArity.OneOrMore };
            serializerLibraries.FromAmong(typeof(SerializerLibraries).GetEnumNames());
            serializerLibraries.SetDefaultValue(SerializerLibraries.YamlDotNet);
            
            var rootCommand = new RootCommand("Tool for automatically generating YML serialization classes from schema files.");
            rootCommand.AddOption(schemaPath);
            rootCommand.AddOption(generatorNamespace);
            rootCommand.AddOption(generatorTypeName);
            rootCommand.AddOption(outputPath);
            rootCommand.AddOption(serializerLibraries);
            rootCommand.SetHandler(async (filePath, generatorNamespace, generatorTypeName, outputFilePath, serializerLibraries) =>
            {
                var schema = await JsonSchema.FromFileAsync(filePath.FullName);
                if (string.IsNullOrEmpty(generatorTypeName))
                {
                    if (!schema.HasTypeNameTitle)
                    {
                        Console.Error.WriteLine("No root name is specified and schema has no title that can be used as type name.");
                        return;
                    }

                    generatorTypeName = schema.Title;
                }

                var settings = new CSharpCodeDomGeneratorSettings
                {
                    Namespace = generatorNamespace,
                    SerializerLibraries = serializerLibraries
                };

                schema = schema.WithUniqueDiscriminatorProperties();
                var generator = new CSharpCodeDomGenerator(schema, settings);
                var code = generator.GenerateFile(generatorTypeName);
                if (string.IsNullOrEmpty(outputFilePath))
                {
                    outputFilePath = $"{generatorNamespace}.Generated.cs";
                }

                Console.WriteLine($"Writing schema classes to {outputFilePath}...");
                File.WriteAllText(outputFilePath, code);
            }, schemaPath, generatorNamespace, generatorTypeName, outputPath, serializerLibraries);
            await rootCommand.InvokeAsync(args);
        }
    }
}
