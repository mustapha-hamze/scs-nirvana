using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.GeneralDtos
{
    public class SystemTypeDto : BaseEntity
    {
        public int ApplicationId { get; set; }

        public int TypeGroupId { get; set; }

        [StringLength(128)]
        public string Title { get; set; }
    }
}