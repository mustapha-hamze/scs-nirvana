using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.CMSDtos
{
    public class FarsiContentMetadataEditDto
    {
        public int Id { get; set; }
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
