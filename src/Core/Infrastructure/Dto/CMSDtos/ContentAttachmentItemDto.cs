namespace Infrastructure.Dto.CMSDtos;
public class ContentAttachmentItemDto : BaseEntity
{
    public int AttachmentId { get; set; }
    [StringLength(64)]
    public string Title { get; set; }

    [StringLength(1024)]
    public string Description { get; set; }

    [StringLength(512)]
    public string FileName { get; set; }
}