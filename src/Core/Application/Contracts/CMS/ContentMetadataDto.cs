using System.ComponentModel.DataAnnotations;
using Domains.Entities;

namespace Application.Contracts.CMS
{
    public class ContentMetadataDto : BaseEntity
    {
        public int ContentId { get; set; }

        [StringLength(256)]
        public string Title { get; set; }

        [StringLength(128)]
        public string Author { get; set; }

        [StringLength(1024)]
        public string Keywords { get; set; }

        [StringLength(2048)]
        public string Description { get; set; }
    }
}
