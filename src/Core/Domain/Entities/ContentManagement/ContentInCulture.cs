using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domains.Entities.ContentManagement
{
    public class ContentInCulture
    {
        public int Id { get; set; }
        public int ContentId { get; set; }
        public int CultureId { get; set; }
    }
}