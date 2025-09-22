using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.PropTechDtos
{
    public class PropertyAttachmentItemDto : BaseEntity
    {
        public int AttachmentId { get; set; }
        [StringLength(64)]
        public string Title { get; set; }

        [StringLength(1024)]
        public string Description { get; set; }

        [StringLength(128)]
        public string FileName { get; set; }
    }
}