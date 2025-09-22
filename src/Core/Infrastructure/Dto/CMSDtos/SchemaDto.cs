namespace Infrastructure.Dto.CMSDtos
{
    public class SchemaDto : BaseEntity
    {
        public int ApplicationId { get; set; }
        public string Title { get; set; }
        public string LogoFileName { get; set; }
    }
}