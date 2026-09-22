using System.Collections.Generic;
using Domains.Entities.General;

namespace Domains.Entities.AccessManagement;
public class Sector : BaseEntity
{
    public int ApplicationId { get; set; }
    public string Title { get; set; }
    // relation
    public virtual Application Application { get; set; }
    public virtual ICollection<SectorEntity> SectorEntities { get; set; }
}