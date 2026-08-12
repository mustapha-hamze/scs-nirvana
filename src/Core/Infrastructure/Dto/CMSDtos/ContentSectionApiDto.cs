using System.Collections.Generic;

namespace Infrastructure.Dto.CMSDtos
{
    public class ContentSectionApiDto : BaseEntity
    {
        public int ContentId { get; set; }
        public int Priority { get; set; }
        public List<SectionElementApiDto> Elements { get; set; }
    }
}
