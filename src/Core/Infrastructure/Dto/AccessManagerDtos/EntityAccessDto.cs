using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.AccessManagerDtos
{
    public class EntityAccessDto : BaseEntity
    {
        // public int ApplicationId { get; set; }
        public int EntityId { get; set; }

        [StringLength(1024)]
        public string Access { get; set; }
    }
}