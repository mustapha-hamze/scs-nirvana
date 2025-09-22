using System.Collections.Generic;
using Infrastructure.Dto.PropTechDtos;

namespace Infrastructure.Dto.SPDtos
{
    public class AlgoliaListDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int DistrictId { get; set; }
        public string DevelopmentTypeTitle { get; set; }
        public string Mapdim { get; set; }
        public string CompletionYearTitle { get; set; }
        public string DistrictTitle { get; set; }
        public string DistrictTitleTwo { get; set; }
        public string DistrictTitleFull { get; set; }
        public string BedRoom { get; set; }
        public string Area { get; set; }
        public int StartingPrice { get; set; }
        public string StartingPriceTitle { get; set; }
        public string ImageUrl { get; set; }
        public int CountryId { get; set; }
        public string CountryTitle { get; set; }
        public string ZoneUrl { get; set; }
        public int MinBedRoom { get; set; }
        public string Zone_1 { get; set; }
        public string Zone_2 { get; set; }
        public string Zone_3 { get; set; }
        public string Zone_4 { get; set; }
        public string RefEx1 { get; set; }
        public string RefEx2 { get; set; }
        public string RefEx3 { get; set; }
        // public List<AmenityDto> Amenities { get; set; }
        public string Amenities { get; set; }

        //public List<AmenityDto> _Amenities { get; set; }
    }
}