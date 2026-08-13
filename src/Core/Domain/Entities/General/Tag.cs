using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domains.Entities.General
{
    public class Tag : BaseEntity
    {
        // property
        public int ApplicationId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }

        public int TypeId { get; set; }

        // relation
        [ForeignKey("ApplicationId")]
        public virtual Application Application { get; set; }
    }
}
