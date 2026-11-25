using System.CommandLine;
using System.Text;

namespace Bonsai.Sgen
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            var schemaPathArgument = new SchemaPathArgument();
            var generatorNamespaceOption = new NamespaceOption();
            var generatorTypeNameOption = new Option<string?>("--root")
            {
                Description = "Type name hint to use for the schema root element. If not specified, the title " +
                              "of the schema will be used."
            };

            var outputPathOption = new Option<string>("--output", "-o")
            {
                Description = "Location to place the generated output. The default is the current directory."
            };

            var nameOption = new Option<string>("--name", "-n")
            {
                Description = "Name of the generated output file. If not specified, the namespace will be used."
            };

            var standardOutputOption = new Option<bool>("--stdout")
            {
                Description = "Writes the generated output to the standard output instead of a file."
            };

            var serializerLibrariesOption = new Option<SerializerOptions>("--serializer")
            {
                Description = "Specifies the serializer data annotations to include in the generated classes.",
                AllowMultipleArgumentsPerToken = true,
                Arity = ArgumentArity.OneOrMore,
                CustomParser = result =>
                {
                    SerializerOptions serializers = default;
                    foreach (var token in result.Tokens)
                    {
                        serializers |= (SerializerOptions)Enum.Parse(typeof(SerializerOptions), token.Value);
                    }
                    return serializers;
                }
            }.AcceptOnlyFromAmong(typeof(SerializerOptions).GetEnumNames());
            
            var rootCommand = new RootCommand("Tool for automatically generating serialization classes from JSON Schema files.");
            rootCommand.Arguments.Add(schemaPathArgument);
            rootCommand.Options.Add(generatorNamespaceOption);
            rootCommand.Options.Add(generatorTypeNameOption);
            rootCommand.Options.Add(outputPathOption);
            rootCommand.Options.Add(nameOption);
            rootCommand.Options.Add(serializerLibrariesOption);
            rootCommand.Options.Add(standardOutputOption);
            rootCommand.Validators.Add(result =>
            {
                if (!result.GetValue(standardOutputOption))
                    return;

                foreach (var option in new[] { outputPathOption, nameOption })
                {
                    if (result.GetResult(option) is not null)
                        result.AddError($"Options '{standardOutputOption.Name}' and '{option.Name}' cannot be used together.");
                }
            });
            rootCommand.SetAction(async (parseResult, cancellationToken) =>
            {
                var referenceResolverFactory = JsonSchemaExtensions.CreateJsonReferenceResolverFactory();
                var schemaDocument = await schemaPathArgument.LoadSchemaAsync(parseResult, referenceResolverFactory, cancellationToken);
                var generatorNamespace = parseResult.GetValue(generatorNamespaceOption) ?? schemaDocument.Name;
                var schema = schemaDocument.Schema;

                var settings = new CSharpCodeDomGeneratorSettings();
                var nameGenerator = (CSharpTypeNameGenerator)settings.TypeNameGenerator;
                settings.SerializerLibraries = (SerializerLibraries)parseResult.GetValue(serializerLibrariesOption);
                settings.Namespace = nameGenerator.GenerateNamespace(schema, generatorNamespace);
                schema = schema.WithCompatibleDefinitions(nameGenerator)
                               .WithResolvedAnyOfNullableProperty()
                               .WithResolvedDiscriminatorInheritance(settings);
                var generator = new CSharpCodeDomGenerator(schema, settings);

                var generatorTypeName = parseResult.GetValue(generatorTypeNameOption);
                if (string.IsNullOrEmpty(generatorTypeName) && schema.HasTypeNameTitle)
                {
                    generatorTypeName = schema.Title;
                }

                var code = generator.GenerateFile(generatorTypeName);

                if (parseResult.GetValue(standardOutputOption))
                {
                    using var writer = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
                    writer.Write(code);
                    return;
                }

                var outputFilePath = parseResult.GetValue(nameOption);
                if (string.IsNullOrEmpty(outputFilePath))
                    outputFilePath = $"{settings.Namespace}.Generated.cs";

                var outputPath = parseResult.GetValue(outputPathOption);
                if (!string.IsNullOrEmpty(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                    outputFilePath = Path.Combine(outputPath, outputFilePath);
                }

                outputFilePath = Path.ChangeExtension(outputFilePath, ".cs");
                Console.WriteLine($"Writing schema classes to {outputFilePath}...");
                File.WriteAllText(outputFilePath, code);
            });

            var parseResult = rootCommand.Parse(args);
            return await parseResult.InvokeAsync();
        }
    }

    [Flags]
    enum SerializerOptions
    {
        json = SerializerLibraries.NewtonsoftJson,
        yaml = SerializerLibraries.YamlDotNet
    }
}
