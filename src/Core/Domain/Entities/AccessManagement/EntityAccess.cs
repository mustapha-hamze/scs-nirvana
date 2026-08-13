namespace Domains.Entities.AccessManagement;
public class EntityAccess : BaseEntity
{
    // public int ApplicationId { get; set; }

    public int EntityId { get; set; }

    [StringLength(1024)]
    public string Access { get; set; }

    // relation
    [ForeignKey("EntityId")]
    public virtual SectorEntity SectorEntity { get; set; }
}