using System.Collections.Generic;
using Domains.Entities;

namespace Application.Contracts.CMSApi
{
    public class ContentSectionApiDto : BaseEntity
    {
        public int ContentId { get; set; }
        public int Priority { get; set; }
        public List<SectionElementApiDto> Elements { get; set; }
    }
}
