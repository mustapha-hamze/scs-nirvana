namespace Infrastructure.Dto.SPDtos
{
    public class LatestRateDto
    {
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; }
        public string Symbol { get; set; }
        public double Value { get; set; }
        public string CountryName { get; set; }
    }
}