using System.Text.Json.Serialization;

namespace Domains.Entities.ContentManagement
{
    public class ContentMetadata : BaseEntity
    {
        public int ContentId { get; set; }

        public string Title { get; set; }

        public string Author { get; set; }

        public string Keywords { get; set; }

        public string Description { get; set; }

        // relations
        [JsonIgnore]
        public virtual Content Content { get; set; }
    }
}