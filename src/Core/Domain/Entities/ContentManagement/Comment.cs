namespace Domains.Entities.ContentManagement;

public class Comment : BaseEntity
{
    public int ContentId { get; set; }

    public int ParentId { get; set; }

    public string Text { get; set; }

    public string OwnerId { get; set; }
}
