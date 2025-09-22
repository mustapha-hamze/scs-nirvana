using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.AccessManagerDtos
{
    public class SectorDto : BaseEntity
    {
        public int ApplicationId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }
    }
}