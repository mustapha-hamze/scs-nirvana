using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domains.Entities.General
{
    public class UserAccess : BaseEntity
    {
        [StringLength(450)]
        public string UserId { get; set; }

        public int ApplicationId { get; set; }

        [StringLength(4096)]
        public string Access { get; set; }
    }
}