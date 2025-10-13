using System.Collections.Generic;
using Domains.Entities.General;

namespace Domains.Entities.AccessManagement;
[Table("AME_Sectors")]
public class Sector : BaseEntity
{
    // public int ApplicationId { get; set; }
    [StringLength(64)]
    public string Title { get; set; }
    // relation
    [ForeignKey("ApplicationId")]
    public virtual Application Application { get; set; }
    public virtual ICollection<SectorEntity> SectorEntities { get; set; }
}