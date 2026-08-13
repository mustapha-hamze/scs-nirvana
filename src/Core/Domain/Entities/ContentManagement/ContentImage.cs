using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Domains.Entities.ContentManagement
{
    public class ContentImage : BaseEntity
    {
        public int ContentId { get; set; }

        [StringLength(128)]
        public string ImageFileName { get; set; }

        [ForeignKey("ContentId")]
        [JsonIgnore]
        public virtual Content Content { get; set; }

        public int Size { get; set; }
    }
}