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
            return PascalCaseNamingConvention.Instance.Apply(value).Replace("_", string.Empty);
        }
    }
}
