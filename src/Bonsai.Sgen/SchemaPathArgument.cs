using System.CommandLine;
using NJsonSchema;
using NJsonSchema.References;

namespace Bonsai.Sgen
{
    internal class SchemaPathArgument : Argument<string>
    {
        public SchemaPathArgument()
            : base("schema")
        {
            Description = "Specifies the URL or path to the JSON schema describing the data types " +
                          "for which to generate serialization classes.";
            Arity = Console.IsInputRedirected ? ArgumentArity.ZeroOrOne : ArgumentArity.ExactlyOne;
            Validators.Add(result =>
            {
                var schemaPath = result.GetValueOrDefault<string>();
                if (schemaPath is not null && !IsUrl(schemaPath) && !File.Exists(schemaPath))
                {
                    result.AddError($"File does not exist: '{schemaPath}'.");
                }
            });
        }

        public async Task<SchemaDocument> LoadSchemaAsync(
            ParseResult parseResult,
            Func<JsonSchema, JsonReferenceResolver> referenceResolverFactory,
            CancellationToken cancellationToken)
        {
            var schemaPath = parseResult.GetValue(this);
            if (schemaPath is null && Console.IsInputRedirected)
            {
                using var stream = Console.OpenStandardInput();
                var inputSchema = await JsonSchema.FromJsonAsync(stream, documentPath: null, referenceResolverFactory, cancellationToken);
                return new SchemaDocument(inputSchema, !string.IsNullOrEmpty(inputSchema.Title) ? inputSchema.Title : "DataSchema");
            }

            var schema = IsUrl(schemaPath!)
                ? await JsonSchema.FromUrlAsync(schemaPath!, referenceResolverFactory, cancellationToken)
                : await JsonSchema.FromFileAsync(Path.GetFullPath(schemaPath!), referenceResolverFactory, cancellationToken);
            return new SchemaDocument(schema, GetSchemaName(schemaPath!));
        }

        public record SchemaDocument(JsonSchema Schema, string Name);

        static string GetSchemaName(string schemaPath)
        {
            var path = IsUrl(schemaPath) ? Uri.UnescapeDataString(new Uri(schemaPath).AbsolutePath) : schemaPath;
            return Path.GetFileNameWithoutExtension(path);
        }

        static bool IsUrl(string schemaPath)
        {
            return Uri.TryCreate(schemaPath, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}
