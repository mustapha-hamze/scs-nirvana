namespace Domains.Entities.AccessManagement;
public class EntityAccess : BaseEntity
{
    // public int ApplicationId { get; set; }

    public int EntityId { get; set; }

    public string Access { get; set; }

    // relation
    public virtual SectorEntity SectorEntity { get; set; }
}