using System.Collections.Generic;

namespace Infrastructure.Dto.SPDtos
{
    public class PropertyOfDevelopmentDto
    {
        public int Id { get; set; }
        public string TypeTitle { get; set; }
        public int DevelopmentId { get; set; }
        public string DevelopmentTitle { get; set; }
        public string Title { get; set; }
        public int BedRoom { get; set; }
        public string BedRoomDescription { get; set; }
        public int BathRoom { get; set; }
        public int MinArea { get; set; }
        public int MaxArea { get; set; }
        public string ReferenceArea { get; set; }

        public List<PropertyOfDevelopmentImageDto> Images { get; set; }
        public List<PropertyOfDevelopmentGalleryDto> Gallery { get; set; }
        public PropertyOfDevelopmentPriceDto Price { get; set; }
    }
}