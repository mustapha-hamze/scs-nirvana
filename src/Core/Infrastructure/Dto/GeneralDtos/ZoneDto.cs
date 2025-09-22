using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.GeneralDtos
{
    public class ZoneDto : BaseEntity
    {
        // public int ApplicationId { get; set; }

        public int ParentId { get; set; }
        [StringLength(64)]
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Initial { get; set; }
        public string ParentTitle { get; set; }
    }
}