namespace Infrastructure.Dto.CMSDtos;
public class ContentAttachmentDto : BaseEntity
{
    public int ContentId { get; set; }

    [StringLength(64)]
    public string Title { get; set; }

    public int Type { get; set; }
}