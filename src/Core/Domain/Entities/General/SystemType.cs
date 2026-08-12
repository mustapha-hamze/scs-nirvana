using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domains.Entities.General
{
    public class SystemType : BaseEntity
    {
        public int ApplicationId { get; set; }
        public int TypeGroupId { get; set; }

        [StringLength(128)]
        public string Title { get; set; }

        public bool IsRTL { get; set; }

        // relation
        [ForeignKey("ApplicationId")]
        public virtual Application Application { get; set; }
    }
}