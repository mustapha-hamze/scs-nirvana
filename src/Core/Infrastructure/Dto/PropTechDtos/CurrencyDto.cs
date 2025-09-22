using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.GeneralDtos
{
    public class CurrencyDto : BaseEntity
    {

        public int CountryId { get; set; }

        [StringLength(6)]
        public string CurrencyCode { get; set; }

        [StringLength(64)]
        public string Name { get; set; }

        [StringLength(6)]
        public string Symbol { get; set; }

        [StringLength(6)]
        public string CountryCode { get; set; }

        [StringLength(64)]
        public string CountryName { get; set; }

        [StringLength(256)]
        public string Slug { get; set; }
    }
}