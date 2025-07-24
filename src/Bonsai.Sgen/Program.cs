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

            var generatorNamespaceOption = new Option<string?>("--namespace", "-ns")
            {
                Description = "The namespace to use for all generated serialization classes. If not specified, " +
                              "the type name hint of the root element will be used, if available."
            };

            var generatorTypeNameOption = new Option<string?>("--root")
            {
                Description = "Type name hint to use for the schema root element. If not specified, the title " +
                              "of the JSON schema will be used, if available."
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
                FileInfo? schemaPath;
                if (Console.IsInputRedirected)
                {
                    schemaPath = null;
                    using var stream = Console.OpenStandardInput();
                    schema = await JsonSchema.FromJsonAsync(stream, cancellationToken);
                }
                else
                {
                    schemaPath = parseResult.GetRequiredValue(schemaPathArgument);
                    schema = Uri.IsWellFormedUriString(schemaPath.FullName, UriKind.Absolute)
                        ? await JsonSchema.FromUrlAsync(schemaPath.FullName, cancellationToken)
                        : await JsonSchema.FromFileAsync(schemaPath.FullName, cancellationToken);
                }

                var settings = new CSharpCodeDomGeneratorSettings();
                var nameGenerator = (CSharpTypeNameGenerator)settings.TypeNameGenerator;
                settings.SerializerLibraries = parseResult.GetValue(serializerLibrariesOption);

                schema = schema.WithCompatibleDefinitions(nameGenerator)
                               .WithResolvedDiscriminatorInheritance();
                var generator = new CSharpCodeDomGenerator(schema, settings);

                var generatorTypeName = parseResult.GetValue(generatorTypeNameOption);
                if (string.IsNullOrEmpty(generatorTypeName))
                {
                    generatorTypeName = generator.Resolver.Resolve(schema, false, schema.Title);
                    if (nameGenerator.ReservedTypeNames.Contains(generatorTypeName))
                        generatorTypeName =
                            schema.HasTypeNameTitle ? schema.Title :
                            schemaPath is not null ? Path.GetFileNameWithoutExtension(schemaPath.Name) :
                            "DataSchema";
                }

                generatorTypeName = nameGenerator.Generate(schema, generatorTypeName, nameGenerator.ReservedTypeNames);
                var generatorNamespace = parseResult.GetValue(generatorNamespaceOption);
                if (string.IsNullOrEmpty(generatorNamespace))
                    generatorNamespace = generatorTypeName;

                settings.Namespace = generatorNamespace;
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
