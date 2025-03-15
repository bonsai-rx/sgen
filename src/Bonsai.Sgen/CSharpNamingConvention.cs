using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Bonsai.Sgen
{
    internal class CSharpNamingConvention : INamingConvention
    {
        public static readonly INamingConvention Instance = new CSharpNamingConvention();

        private CSharpNamingConvention()
        {
        }

        public string Apply(string value)
        {
            var result = PascalCaseNamingConvention.Instance.Apply(value);
            var prefix = result.StartsWith('_') ? "_" : string.Empty;
            return prefix + result.Replace("_", string.Empty);
        }

        public string Reverse(string value)
        {
            throw new NotSupportedException();
        }
    }
}
