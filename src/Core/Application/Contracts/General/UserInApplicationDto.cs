using System.ComponentModel.DataAnnotations;
using Domains.Entities;

namespace Application.Contracts.General
{
    public class UserInApplicationDto : BaseEntity
    {
        [StringLength(450)]
        public string UserId { get; set; }
        public int ApplicationId { get; set; }
    }
}
