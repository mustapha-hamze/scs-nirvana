using Domains.Entities.General;

namespace Domains.Entities.ContentManagement;

public class Category : BaseEntity
{
    // property
    public int ApplicationId { get; set; }

    public int ParentId { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    // relation
    public virtual Application Application { get; set; }
}