using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.GeneralDtos
{
    public class UserInApplicationDto : BaseEntity
    {
        [StringLength(450)]
        public string UserId { get; set; }
        public int ApplicationId { get; set; }
    }
}