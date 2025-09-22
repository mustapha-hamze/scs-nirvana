using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.PropTechDtos
{
    public class PropertyAttachmentDto : BaseEntity
    {
        public int PropertyId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }

        public int Type { get; set; }
    }
}