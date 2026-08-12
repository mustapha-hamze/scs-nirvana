namespace Domains.Entities.ContentManagement;

public class Comment : BaseEntity
{
    public int ContentId { get; set; }

    public int ParentId { get; set; }

    [StringLength(1024)]
    public string Text { get; set; }

    [StringLength(450)]
    public string OwnerId { get; set; }
}
