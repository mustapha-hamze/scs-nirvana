using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.CMSDtos
{
    public class SaveContentBodyDto
    {
        public int SectionId { get; set; }
        public int ContentId { get; set; }
        public int Priority { get; set; }
        public List<ContentBodyElementDto> Elements { get; set; }
    }
    public class ContentBodyElementDto
    {
        public int Id { get; set; }
        public int ElementType { get; set; }
        public string Value { get; set; }
        public int ContentId { get; set; }
        public int Size { get; set; }
        public string Title { get; set; }
    }
}