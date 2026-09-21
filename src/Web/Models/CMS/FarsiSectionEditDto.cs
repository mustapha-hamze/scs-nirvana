using System.Collections.Generic;

namespace Web.Models.CMS
{
    public class FarsiSectionEditDto
    {
        public int Id { get; set; }
        public int ContentId { get; set; }
        public int Priority { get; set; }
        public List<FarsiSectionElementEditDto> SectionElements { get; set; }
    }
}
