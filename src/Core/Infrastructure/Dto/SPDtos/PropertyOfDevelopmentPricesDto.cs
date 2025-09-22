using System;

namespace Infrastructure.Dto.SPDtos
{
    public class PropertyOfDevelopmentPriceDto
    {
        public string MainPrice { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; }
        public DateTime CreatedDT { get; set; }
    }
}