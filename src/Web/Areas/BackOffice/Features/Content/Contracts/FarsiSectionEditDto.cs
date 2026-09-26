using System.Collections.Generic;

namespace Web.Areas.BackOffice.Features.Content.Contracts
{
    public class FarsiSectionEditDto
    {
        public int Id { get; set; }
        public List<FarsiSectionElementEditDto> SectionElements { get; set; }
    }
}
