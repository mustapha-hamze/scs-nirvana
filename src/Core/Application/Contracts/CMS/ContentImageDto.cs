using System.ComponentModel.DataAnnotations;
using Domains.Entities;

namespace Application.Contracts.CMS
{
    public class ContentImageDto : BaseEntity
    {
        public int ContentId { get; set; }

        [StringLength(128)]
        public string ImageFileName { get; set; }

        public int Size { get; set; }
    }
}
