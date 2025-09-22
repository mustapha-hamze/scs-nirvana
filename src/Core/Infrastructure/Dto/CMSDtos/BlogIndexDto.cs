using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domains.Entities.ContentManagement;

namespace Infrastructure.Dto.CMSDtos
{
    public class BlogIndexDto
    {
        public List<Content> Contents { get; set; }
        public int PagesCount { get; set; }
        public int PageIndex { get; set; }
    }
}