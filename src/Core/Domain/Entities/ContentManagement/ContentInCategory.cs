using System;

namespace Domains.Entities.ContentManagement
{
    public class ContentInCategory
    {
        public int Id { get; set; }
        public int ContentId { get; set; }
        public int CategoryId { get; set; }
        public DateTime CreatedDt { get; set; }
    }
}