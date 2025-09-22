using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.SCMDtos
{
    public class SliderDto : BaseEntity
    {
        public int ApplicationId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }

        [StringLength(512)]
        public string Description { get; set; }
    }
}