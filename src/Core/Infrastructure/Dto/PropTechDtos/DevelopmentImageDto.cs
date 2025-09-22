using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Dto.PropTechDtos
{
    public class DevelopmentImageDto : BaseEntity
    {
        public int DevelopmentId { get; set; }

        [StringLength(128)]
        public string ImageFileName { get; set; }

        [ForeignKey("DevelopmentId")]
        public virtual DevelopmentDto Development { get; set; }
    }
}