using System.Collections.Generic;

namespace Infrastructure.Dto.CMSDtos
{
    public class FarsiSectionEditDto
    {
        public int Id { get; set; }
        public int ContentId { get; set; }
        public int Priority { get; set; }
        public List<FarsiSectionElementEditDto> SectionElements { get; set; }
    }
}
