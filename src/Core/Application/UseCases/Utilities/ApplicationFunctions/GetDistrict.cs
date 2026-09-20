namespace Services.Utilities.ApplicationFunctions
{
    public class GetDistrict
    {
        public GetDistrict()
        {

        }

        public string[] GetTitles(string fullTitle)
        {
            var result = fullTitle.Split(" -> ");
            return result;
        }
    }
}