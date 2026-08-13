using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.CMSDtos
{
    public class ContentDto : BaseEntity
    {
        // property
        public int ApplicationId { get; set; }

        public int TypeId { get; set; }

        [StringLength(256)]
        public string Title { get; set; }

        [StringLength(2048)]
        public string HeadLine { get; set; }

        [StringLength(2048)]
        public string Abstract { get; set; }

        public string Description { get; set; }

        [StringLength(1024)]
        public string Categories { get; set; }
        [StringLength(1024)]
        public string Tags { get; set; }
        [StringLength(1024)]
        public string Cultures { get; set; }

        public DateTime PublishDt { get; set; }
    }
}