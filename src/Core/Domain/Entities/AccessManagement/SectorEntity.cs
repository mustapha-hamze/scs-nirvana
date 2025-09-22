using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domains.Entities.General;

namespace Domains.Entities.AccessManagement
{
    [Table("AME_SectorEntities")]
    public class SectorEntity : BaseEntity
    {
        public int SectorId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }

        [StringLength(256)]
        public string AccessKey { get; set; }


        // relation
        [ForeignKey("SectorId")]
        public virtual Sector Sector { get; set; }

        public virtual ICollection<EntityAccess> EntityAccesses { get; set; }
    }
}