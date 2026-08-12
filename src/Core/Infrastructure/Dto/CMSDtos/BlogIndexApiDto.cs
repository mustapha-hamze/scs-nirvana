using System.Collections.Generic;

namespace Infrastructure.Dto.CMSDtos
{
    public class BlogIndexApiDto
    {
        public List<ContentApiDto> Contents { get; set; }
        public int PagesCount { get; set; }
        public int PageIndex { get; set; }
    }
}
