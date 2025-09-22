using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.PropTechDtos
{
    public class DevelopmentApiDto
    {
        public DevelopmentDto Development { get; set; }
        public List<DevelopmentImageDto> DevelopmentImages { get; set; }
    }
}