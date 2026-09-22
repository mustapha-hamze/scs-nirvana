using System.Text.Json.Serialization;

namespace Domains.Entities.ContentManagement
{
    public class SectionElement : BaseEntity
    {
        // property
        public int SectionId { get; set; }
        public int ElementType { get; set; }

        public string TinyText { get; set; }

        public string EditorText { get; set; }

        public string FileNameText { get; set; }

        public string GalleryImages { get; set; }

        public int Size { get; set; }

        public string ElementTitle { get; set; }

        // foreign key
        [JsonIgnore]
        public virtual ContentSection Section { get; set; }
    }
}
