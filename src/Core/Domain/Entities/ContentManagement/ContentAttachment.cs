namespace Domains.Entities.ContentManagement;
public class ContentAttachment : BaseEntity
{
    public int ContentId { get; set; }

    public string Title { get; set; }

    public int Type { get; set; }

    // relation
    public virtual Content Content { get; set; }

    //public virtual ICollection<DevelopmentAttachmentItem> AttachmentItems { get; set; }
}