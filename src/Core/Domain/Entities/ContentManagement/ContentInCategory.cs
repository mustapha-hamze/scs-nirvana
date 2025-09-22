using System;

namespace Domains.Entities.ContentManagement
{
    [Table("CMS_ContentInCategories")]
    public class ContentInCategory
    {
        [Key]
        public int Id { get; set; }
        public int ContentId { get; set; }
        public int CategoryId { get; set; }
        public DateTime CreatedDt { get; set; }
    }
}