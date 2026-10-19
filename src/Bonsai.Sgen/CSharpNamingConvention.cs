using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;

namespace Bonsai.Sgen
{
    internal class CSharpNamingConvention : INamingConvention
    {
        static readonly Regex Separators = new(@"[\W_]+");

        public static readonly INamingConvention Instance = new CSharpNamingConvention();

        private CSharpNamingConvention()
        {
        }

        public string Apply(string value)
        {
            var result = new StringBuilder(value.Length);
            foreach (var part in Separators.Split(value))
            {
                if (part.Length == 0)
                    continue;

                result.Append(char.ToUpperInvariant(part[0]));
                result.Append(part, 1, part.Length - 1);
            }

            if (result.Length == 0 || !char.IsLetter(result[0]))
                result.Insert(0, '_');
            return result.ToString();
        }

        public string Reverse(string value)
        {
            throw new NotSupportedException();
        }
    }
}
