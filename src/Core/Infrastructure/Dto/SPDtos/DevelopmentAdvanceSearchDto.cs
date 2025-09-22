using System.Collections.Generic;
using Infrastructure.Dto.PropTechDtos;

namespace Infrastructure.Dto.SPDtos
{
    public class DevelopmentAdvanceSearchDto
    {
        public List<DevelopmentDto> Developments { get; set; }
        public PagingDto Paging { get; set; }
    }
}