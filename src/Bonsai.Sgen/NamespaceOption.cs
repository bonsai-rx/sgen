using System.CommandLine;

namespace Bonsai.Sgen
{
    internal class NamespaceOption : Option<string>
    {
        public NamespaceOption()
            : base("--namespace")
        {
            Description = "Namespace to use for generated code. If not specified, the sanitized name " +
                          "of the schema file will be used.";
            Validators.Add(result =>
            {
                var generatorNamespace = result.GetValueOrDefault<string>();
                if (string.IsNullOrWhiteSpace(generatorNamespace))
                {
                    result.AddError("The namespace cannot be empty.");
                }
            });
        }
    }
}
