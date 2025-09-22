using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.SCMDtos
{
    public class BoxDto : BaseEntity
    {
        public int ApplicationId { get; set; }


        public int Type { get; set; }

        [StringLength(128)]
        public string Title { get; set; }
    }
}