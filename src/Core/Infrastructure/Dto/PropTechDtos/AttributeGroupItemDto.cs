using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.PropTechDtos
{
    public class AttributeGroupItemDto : BaseEntity
    {
        public int AttributeGroupId { get; set; }
        // public virtual AttributeGroupDto AttributeGroup { get; set; }

        // main fields
        [StringLength(64)]
        public string Title { get; set; }
        [StringLength(512)]
        public string Description { get; set; }

        [StringLength(64)]
        public string FA_Title { get; set; }

        public int AttributeType { get; set; }

        [StringLength(128)]
        public string Icon { get; set; }
    }
}