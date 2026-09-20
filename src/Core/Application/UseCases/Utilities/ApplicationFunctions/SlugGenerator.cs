using System.Text;

namespace Services.Utilities.ApplicationFunctions
{
    public class SlugGenerator
    {
        public string GenerateSlug(string strTitle)
        {
            string result = string.Empty;
            StringBuilder sb = new StringBuilder();
            foreach (char c in strTitle)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == ' ' || c == '-')
                {
                    sb.Append(c);
                }
            }

            result = sb.ToString();
            result = result.Replace(" ", "-");

            result = result.Replace("----", "-");
            result = result.Replace("---", "-");
            result = result.Replace("--", "-");

            return result.ToLower();
        }
    }
}