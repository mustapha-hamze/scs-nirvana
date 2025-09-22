using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.SCMDtos
{
    public class SliderItemDto : BaseEntity
    {
        public int SliderId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }

        [StringLength(512)]
        public string Description { get; set; }

        [StringLength(128)]
        public string ImageFileName { get; set; }
    }
}