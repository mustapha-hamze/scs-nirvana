using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Infrastructure.Dto.PropTechDtos
{
    public class DevelopmentAttachmentDto : BaseEntity
    {
        public int DevelopmentId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }

        public int Type { get; set; }

        [ForeignKey("DevelopmentId")]
        public virtual DevelopmentDto Development { get; set; }

        public virtual ICollection<DevelopmentAttachmentItemDto> AttachmentItems { get; set; }
    }
}