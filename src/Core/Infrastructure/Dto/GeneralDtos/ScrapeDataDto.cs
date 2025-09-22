using System.ComponentModel.DataAnnotations;

namespace Repository.Dto.GeneralDtos
{
    public class ScrapeDataDto
    {
        [StringLength(16)]
        public string WebScraperOrder { get; set; }

        [StringLength(256)]
        public string Title { get; set; }

        [StringLength(128)]
        public string Developer { get; set; }

        [StringLength(1024)]
        public string Address { get; set; }

        [StringLength(32)]
        public string Completion { get; set; }

        [StringLength(32)]
        public string Square { get; set; }

        [StringLength(32)]
        public string DevelopmentType { get; set; }

        [StringLength(10)]
        public string BedRooms { get; set; }

        [StringLength(32)]
        public string BestPrice { get; set; }

        [StringLength(8192)]
        public string Properties { get; set; }

        [StringLength(8192)]
        public string ImagesGallery { get; set; }

        [StringLength(8192)]
        public string Amenities { get; set; }

        [StringLength(8192)]
        public string Description { get; set; }

        [StringLength(4096)]
        public string Schools { get; set; }

        [StringLength(4096)]
        public string BusStop { get; set; }

        [StringLength(4096)]
        public string Subway { get; set; }

        [StringLength(4096)]
        public string Airport { get; set; }

        [StringLength(4096)]
        public string Train { get; set; }

        public int EntralonId { get; set; }
    }
}