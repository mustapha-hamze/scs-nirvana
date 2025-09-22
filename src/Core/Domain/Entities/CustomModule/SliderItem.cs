using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domains.Entities.CustomModule
{
    [Table("SCM_SliderItems")]
    public class SliderItem : BaseEntity
    {
        public int SliderId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }

        [StringLength(2048)]
        public string Description { get; set; }

        [StringLength(256)]
        public string Link { get; set; }

        [StringLength(128)]
        public string ImageFileName { get; set; }


        [ForeignKey("SliderId")]
        public virtual Slider Slider { get; set; }
    }
}