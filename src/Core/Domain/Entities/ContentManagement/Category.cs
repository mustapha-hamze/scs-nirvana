using Domains.Entities.General;

namespace Domains.Entities.ContentManagement;

[Table("CMS_Categories")]
public class Category : BaseEntity
{
    // property
    public int ApplicationId { get; set; }

    public int ParentId { get; set; }

    [StringLength(256)]
    public string Title { get; set; }

    [StringLength(1024)]
    public string Description { get; set; }

    // relation
    [ForeignKey("ApplicationId")]
    public virtual Application Application { get; set; }
}