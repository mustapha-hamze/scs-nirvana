using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.PropTechDtos
{
    public class PropertyImageDto : BaseEntity
    {
        public int PropertyId { get; set; }

        [StringLength(128)]
        public string ImageFileName { get; set; }
    }
}