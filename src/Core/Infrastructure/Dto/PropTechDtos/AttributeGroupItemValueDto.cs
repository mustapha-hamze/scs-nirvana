using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.PropTechDtos
{
    public class AttributeGroupItemValueDto : BaseEntity
    {
        // relation
        public int AttributeId { get; set; }
        public int ReferenceId { get; set; } // propertyId or developmentId

        public bool BoolValue { get; set; }
        [StringLength(128)]
        public string TextValue { get; set; }
    }
}