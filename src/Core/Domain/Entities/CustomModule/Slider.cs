using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domains.Entities.General;

namespace Domains.Entities.CustomModule
{
    [Table("SCM_Sliders")]
    public class Slider : BaseEntity
    {
        public int ApplicationId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }

        [ForeignKey("ApplicationId")]
        public virtual Application Application { get; set; }

        public virtual ICollection<SliderItem> SliderItems { get; set; }
    }
}