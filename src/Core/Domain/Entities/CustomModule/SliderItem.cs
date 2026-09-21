namespace Domains.Entities.CustomModule
{
    public class SliderItem : BaseEntity
    {
        public int SliderId { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Link { get; set; }

        public string ImageFileName { get; set; }


        public virtual Slider Slider { get; set; }
    }
}