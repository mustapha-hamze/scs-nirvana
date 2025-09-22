using System.Collections.Generic;

namespace Infrastructure.Dto.CMSDtos
{
    public class ContentApiDto
    {
        public int PageIndex { get; set; }
        public int PageCount { get; set; }
        public List<ContentDto> Contents { get; set; }
    }
}