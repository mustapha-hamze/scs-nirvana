using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.CMSDtos
{
    public class FarsiSectionElementEditDto
    {
        public int Id { get; set; }
        public int SectionId { get; set; }
        public int ElementType { get; set; }

        [StringLength(256)]
        public string TinyText { get; set; }

        public string EditorText { get; set; }

        [StringLength(256)]
        public string FileNameText { get; set; }

        [StringLength(4092)]
        public string GalleryImages { get; set; }

        public string ElementTitle { get; set; }
        public int Size { get; set; }
    }
}
