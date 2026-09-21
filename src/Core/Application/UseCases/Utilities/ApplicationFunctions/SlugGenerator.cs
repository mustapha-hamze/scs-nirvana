using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Application.UseCases.Utilities.ApplicationFunctions
{
    public class SlugGenerator
    {
        // Keeps letters/digits of any script (Persian included) instead of an ASCII-only
        // whitelist, which previously dropped non-Latin titles to a near-empty slug.
        public string GenerateSlug(string strTitle)
        {
            if (string.IsNullOrEmpty(strTitle))
                return string.Empty;

            string normalized = strTitle.Normalize(NormalizationForm.FormC);

            var sb = new StringBuilder();
            foreach (char c in normalized)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLower(c, CultureInfo.InvariantCulture));
                else
                    sb.Append('-');
            }

            string result = Regex.Replace(sb.ToString(), "-+", "-").Trim('-');

            return result;
        }
    }
}