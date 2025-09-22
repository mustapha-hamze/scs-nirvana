using System.Collections.Generic;

namespace Infrastructure.Dto.CMSDtos
{
    public class SectionDto : BaseEntity
    {
        public int ContentId { get; set; }

        public int Priority { get; set; }

        public List<SectionElementDto> SectionElements { get; set; }
    }
}