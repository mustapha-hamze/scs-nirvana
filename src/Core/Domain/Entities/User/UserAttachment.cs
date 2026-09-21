using Domains.Entities;

namespace Domains.Entities.User;

public class UserAttachment : BaseEntity
{
    public string UserId { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public int AttachmentType { get; set; }
}
