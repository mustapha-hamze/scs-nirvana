namespace Infrastructure.Extensions
{
    public static class StringExtensions
    {
        public static string RemoveApplicationIdForReseller(this string input, string appId, string key)
        {
            return input.Replace($"{appId}_{key}_", "");
        }
        public static string AddApplicationIdForReseller(this string input, string appId, string key)
        {
            return $"{appId}_{key}_{input}";
        }
    }
}