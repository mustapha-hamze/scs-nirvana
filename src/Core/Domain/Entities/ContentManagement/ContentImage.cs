using System.Text.Json.Serialization;

namespace Domains.Entities.ContentManagement
{
    public class ContentImage : BaseEntity
    {
        public int ContentId { get; set; }

        public string ImageFileName { get; set; }

        [JsonIgnore]
        public virtual Content Content { get; set; }

        public int Size { get; set; }
    }
}