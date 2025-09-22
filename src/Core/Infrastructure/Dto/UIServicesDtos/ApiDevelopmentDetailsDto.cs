using System.Collections.Generic;

namespace Infrastructure.Dto.UIServicesDtos
{
    public class DevelopmentGalleryDto
    {
        public string FileName { get; set; }
    }
    public class ApiDevelopmentDetailsDto : ApiDevelopmentDto
    {
        public List<DevelopmentGalleryDto> GeneralGallery { get; set; }
        public List<DevelopmentGalleryDto> FloorGallery { get; set; }
        public List<ApiPropertyDto> Properties { get; set; }
    }
}