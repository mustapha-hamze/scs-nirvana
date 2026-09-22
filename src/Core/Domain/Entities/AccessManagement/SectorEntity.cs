using System.Collections.Generic;

namespace Domains.Entities.AccessManagement;
public class SectorEntity : BaseEntity
{
    public int SectorId { get; set; }
    public string Title { get; set; }
    public string AccessKey { get; set; }
    // relation
    public virtual Sector Sector { get; set; }
    public virtual ICollection<EntityAccess> EntityAccesses { get; set; }
}