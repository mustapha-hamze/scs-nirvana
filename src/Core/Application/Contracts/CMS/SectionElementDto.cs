using System.ComponentModel.DataAnnotations;
using Domains.Entities;

namespace Application.Contracts.CMS
{
    public class SectionElementDto : BaseEntity
    {
        public int SectionId { get; set; }

        public int ElementType { get; set; }

        [StringLength(256)]
        public string TinyText { get; set; }

        public string EditorText { get; set; }

        [StringLength(256)]
        public string FileNameText { get; set; }

        public string ElementTitle { get; set; }

        [StringLength(4092)]
        public string GalleryImages { get; set; }
        public int Size { get; set; }
    }
}
