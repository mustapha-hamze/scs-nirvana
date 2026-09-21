namespace Domains.Entities.ContentManagement;
public class ContentAttachmentItem : BaseEntity
{
    public int AttachmentId { get; set; }
    public string Title { get; set; }

    public string Description { get; set; }

    public string FileName { get; set; }

    // relation
    public virtual ContentAttachment Attachment { get; set; }
}