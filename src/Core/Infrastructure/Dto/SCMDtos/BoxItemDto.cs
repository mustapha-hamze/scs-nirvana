using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.SCMDtos
{
    public class BoxItemDto : BaseEntity
    {
        public int EntityId { get; set; }
        public int EntityType { get; set; }

        [StringLength(128)]
        public string EntityTitle { get; set; }

        public int BoxId { get; set; }
    }
}