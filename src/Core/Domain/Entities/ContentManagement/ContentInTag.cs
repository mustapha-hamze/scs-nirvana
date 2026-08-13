using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domains.Entities.ContentManagement
{
    public class ContentInTag
    {
        public int Id { get; set; }
        public int ContentId { get; set; }
        public int TagId { get; set; }
    }
}