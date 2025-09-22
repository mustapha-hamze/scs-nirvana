using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domains.Entities.General
{
    [Table("GNR_UserInApplications")]
    public class UserInApplication : BaseEntity
    {
        [StringLength(450)]
        public string UserId { get; set; }
        public int ApplicationId { get; set; }
    }
}