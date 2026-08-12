namespace Infrastructure.Dto.CMSDtos
{
    public class ContentMetadataApiDto : BaseEntity
    {
        public int ContentId { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public string Keywords { get; set; }
        public string Description { get; set; }
    }
}
