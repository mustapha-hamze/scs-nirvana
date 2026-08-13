using Domains.Entities;

namespace Application.Contracts.CMS
{
    public class SchemaDto : BaseEntity
    {
        public int ApplicationId { get; set; }
        public string Title { get; set; }
        public string LogoFileName { get; set; }
        public int TypeId { get; set; }
    }
}
