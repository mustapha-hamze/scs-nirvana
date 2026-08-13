using System.Collections.Generic;
using Domains.Entities;

namespace Application.Contracts.CMS
{
    public class SectionDto : BaseEntity
    {
        public int ContentId { get; set; }

        public int Priority { get; set; }

        public List<SectionElementDto> SectionElements { get; set; }
    }
}
