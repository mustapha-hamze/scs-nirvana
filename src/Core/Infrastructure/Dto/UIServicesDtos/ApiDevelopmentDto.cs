using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Infrastructure.Dto.PropTechDtos;


namespace Infrastructure.Dto.UIServicesDtos
{
    public class ApiDevelopmentDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int DistrictId { get; set; }
        public string DevelopmentTypeTitle { get; set; }
        public string Mapdim { get; set; }
        public string CompletionYearTitle { get; set; }
        public string DistrictTitleFull { get; set; }
        public string BedRoom { get; set; }
        public string ImageUrl { get; set; }
        public int CountryId { get; set; }
        public int CityId { get; set; }
        public int MinBedRoom { get; set; }
        public int MaxBedRoom { get; set; }
        public int StartingPrice { get; set; }
        public string StartingPriceTitle { get; set; }
        public string Area { get; set; }
        public string Zone_1 { get; set; }
        public string Zone_2 { get; set; }
        public string Zone_3 { get; set; }
        public string Zone_4 { get; set; }

        public string ReferenceSite { get; set; }
        public string ReferenceMeta { get; set; }
        public string Abstract { get; set; }
        public string RefEx1 { get; set; }
        public string RefEx2 { get; set; }
        public string RefEx3 { get; set; }
        public string RefProps { get; set; }
        public string RefTitle1 { get; set; }
        public string UITitle { get; set; }
        public bool Featured { get; set; }
        public string LiveSync { get; set; }
        public string LiveSyncModule { get; set; }
        public string LiveSyncUrl { get; set; }
        public List<AmenityDto> Amenities { get; set; }

        public List<DevelopmentImageDto> Images { get; set; }
        public string GoogleMapLocation
        {
            get; set;
        }
    }
}