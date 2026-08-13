using Domains.Entities;

namespace Domains.Entities.User;

public class UserAttachment : BaseEntity
{
    [StringLength(450)]
    public string UserId { get; set; }

    [StringLength(256)]
    public string Title { get; set; }

    public string Description { get; set; }

    public int AttachmentType { get; set; }
}
