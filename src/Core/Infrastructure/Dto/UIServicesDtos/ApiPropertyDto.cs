using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.UIServicesDtos
{
    public class ApiPropertyDto : BaseEntity
    {
        public int ApplicationId { get; set; }

        public int Type { get; set; }
        public string TypeTitle { get; set; }
        public int BedRoom { get; set; }
        [StringLength(128)]
        public string BedRoomDescription { get; set; }
        public int BathRoom { get; set; }
        public int MinArea { get; set; }
        public int MaxArea { get; set; }
        public int MinGrossArea { get; set; }
        public int MaxGrossArea { get; set; }
        public int ReferenceArea { get; set; }
        public int GrossArea { get; set; }
        public int NetArea { get; set; }
        [StringLength(64)]
        public string Title { get; set; }
        public string Description { get; set; }

        // extera
        public int ReferenceId { get; set; }
        public int ReferenceId2 { get; set; }

        //realtion
        public int DevelopmentId { get; set; }
        public int DevelopmentDistrictId { get; set; }
        public int DevelopmentCountryId { get; set; }
        public string DevelopmentTitle { get; set; }

        public string ImageFileName { get; set; }

        public List<DevelopmentGalleryDto> FloorGallery { get; set; }

        public List<ApiPriceDto> Prices { get; set; }
    }
}