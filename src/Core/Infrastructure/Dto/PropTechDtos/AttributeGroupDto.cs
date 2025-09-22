using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.PropTechDtos
{
    public class AttributeGroupDto : BaseEntity
    {
        public int ApplicationId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }
        public int Type { get; set; } // property or development
        [StringLength(512)]
        public string Description { get; set; }

        // relation
        public virtual ICollection<AttributeGroupItemDto> AttributeGroupItems { get; set; }
    }
}