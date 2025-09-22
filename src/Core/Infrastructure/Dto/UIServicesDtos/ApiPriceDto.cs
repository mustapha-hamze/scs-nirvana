using System;

namespace Infrastructure.Dto.UIServicesDtos
{
    public class ApiPriceDto : BaseEntity
    {
        public int PropertyId { get; set; }
        public double MainPrice { get; set; }
        public int CurrencyId { get; set; }
        public double MinPrice { get; set; }
        public double MaxPrice { get; set; }
        public double OfferPrice { get; set; }
        public double PurchasePrice { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int CultureId { get; set; }
        public bool WorldWide { get; set; }
        public bool Offer { get; set; }
        public bool Limited { get; set; }
        public int UserGroupTypeId { get; set; }

        public string PropertyTitle { get; set; }
        public string DevelopmentTitle { get; set; }
        public int DevelopmentId { get; set; }
        public int DevelopmentDistrictId { get; set; }
        public int DevelopmentCountryId { get; set; }
    }
}