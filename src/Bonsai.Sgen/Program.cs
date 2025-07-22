using System.CommandLine;
using NJsonSchema;

namespace Bonsai.Sgen
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            var schemaPathArgument = ArgumentValidation.AcceptExistingOnly(
                new Argument<FileInfo>("schema")
            {
                Description = "Specifies the URL or path to the JSON schema describing the data types " +
                              "for which to generate serialization classes.",
                Arity = Console.IsInputRedirected ? ArgumentArity.Zero : ArgumentArity.ExactlyOne
            });

            var generatorNamespaceOption = new Option<string?>("--namespace")
            {
                DefaultValueFactory = _ => "DataSchema",
                Description = "Specifies the namespace to use for all generated serialization classes."
            };

            var generatorTypeNameOption = new Option<string?>("--root")
            {
                Description = "Specifies the name of the class used to represent the schema root element."
            };

            var outputPathOption = new Option<string>("--output")
            {
                Description = "Specifies the name of the file containing the generated code."
            };

            var serializerLibrariesOption = new Option<SerializerLibraries>("--serializer")
            {
                Description = "Specifies the serializer data annotations to include in the generated classes.",
                DefaultValueFactory = _ => SerializerLibraries.YamlDotNet,
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.OneOrMore,
                CustomParser = result =>
                {
                    SerializerLibraries serializers = default;
                    foreach (var token in result.Tokens)
                    {
                        serializers |= (SerializerLibraries)Enum.Parse(typeof(SerializerLibraries), token.Value);
                    }
                    return serializers;
                }
            }.AcceptOnlyFromAmong(typeof(SerializerLibraries).GetEnumNames());
            
            var rootCommand = new RootCommand("Tool for automatically generating YML serialization classes from schema files.");
            rootCommand.Arguments.Add(schemaPathArgument);
            rootCommand.Options.Add(generatorNamespaceOption);
            rootCommand.Options.Add(generatorTypeNameOption);
            rootCommand.Options.Add(outputPathOption);
            rootCommand.Options.Add(serializerLibrariesOption);
            rootCommand.SetAction(async (parseResult, cancellationToken) =>
            {
                JsonSchema schema;
                if (Console.IsInputRedirected)
                {
                    using var stream = Console.OpenStandardInput();
                    schema = await JsonSchema.FromJsonAsync(stream, cancellationToken);
                }
                else
                {
                    var schemaPath = parseResult.GetRequiredValue(schemaPathArgument);
                    schema = Uri.IsWellFormedUriString(schemaPath.FullName, UriKind.Absolute)
                        ? await JsonSchema.FromUrlAsync(schemaPath.FullName, cancellationToken)
                        : await JsonSchema.FromFileAsync(schemaPath.FullName, cancellationToken);
                }

                var generatorTypeName = parseResult.GetValue(generatorTypeNameOption);
                if (string.IsNullOrEmpty(generatorTypeName))
                {
                    if (!schema.HasTypeNameTitle)
                    {
                        Console.Error.WriteLine("No root name is specified and schema has no title that can be used as type name.");
                        return;
                    }

                    generatorTypeName = schema.Title;
                }

                var generatorNamespace = parseResult.GetValue(generatorNamespaceOption);
                var serializerLibraries = parseResult.GetValue(serializerLibrariesOption);
                var settings = new CSharpCodeDomGeneratorSettings
                {
                    Namespace = generatorNamespace,
                    SerializerLibraries = serializerLibraries
                };

                schema = schema.WithCompatibleDefinitions(settings.TypeNameGenerator)
                               .WithResolvedDiscriminatorInheritance();
                var generator = new CSharpCodeDomGenerator(schema, settings);
                var code = generator.GenerateFile(generatorTypeName);

                var outputFilePath = parseResult.GetValue(outputPathOption);
                if (string.IsNullOrEmpty(outputFilePath))
                {
                    outputFilePath = $"{generatorNamespace}.Generated.cs";
                }

                Console.WriteLine($"Writing schema classes to {outputFilePath}...");
                File.WriteAllText(outputFilePath, code);
            });

            var parseResult = rootCommand.Parse(args);
            return await parseResult.InvokeAsync();
        }
    }
}
