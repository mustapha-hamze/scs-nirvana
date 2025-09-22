namespace Domains.Entities.ContentManagement;
[Table("CMS_ContentAttachmentItems")]
public class ContentAttachmentItem : BaseEntity
{
    public int AttachmentId { get; set; }
    [StringLength(64)]
    public string Title { get; set; }

    [StringLength(1024)]
    public string Description { get; set; }

    [StringLength(512)]
    public string FileName { get; set; }

    // relation
    [ForeignKey("AttachmentId")]
    public virtual ContentAttachment Attachment { get; set; }
}