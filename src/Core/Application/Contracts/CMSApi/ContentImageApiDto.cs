using Domains.Entities;

namespace Application.Contracts.CMSApi
{
    public class ContentImageApiDto : BaseEntity
    {
        public int ContentId { get; set; }
        public string ImageFileName { get; set; }
        public int Size { get; set; }
    }
}
