using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domains.Entities.General
{
    public class SystemLog : BaseEntity
    {
        // property
        public int ApplicationId { get; set; }

        public int OperationCode { get; set; }

        public int EntityId { get; set; }

        [StringLength(450)]
        public string OperationOwner { get; set; }

        // relation
        [ForeignKey("ApplicationId")]
        public virtual Application Application { get; set; }
    }
}
