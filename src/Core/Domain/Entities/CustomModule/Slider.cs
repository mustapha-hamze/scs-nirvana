using System.Collections.Generic;
using Domains.Entities.General;

namespace Domains.Entities.CustomModule
{
    public class Slider : BaseEntity
    {
        public int ApplicationId { get; set; }

        public string Title { get; set; }

        public virtual Application Application { get; set; }

        public virtual ICollection<SliderItem> SliderItems { get; set; }
    }
}