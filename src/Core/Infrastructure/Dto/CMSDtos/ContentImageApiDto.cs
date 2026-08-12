namespace Infrastructure.Dto.CMSDtos
{
    public class ContentImageApiDto : BaseEntity
    {
        public int ContentId { get; set; }
        public string ImageFileName { get; set; }
        public int Size { get; set; }
    }
}
