using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domains.Entities.ContentManagement
{
    [Table("CMS_ContentInCultures")]
    public class ContentInCulture
    {
        [Key]
        public int Id { get; set; }
        public int ContentId { get; set; }
        public int CultureId { get; set; }
    }
}