using Domains.Entities;

namespace Application.Contracts.CMS
{
    public class CategoryDto : BaseEntity
    {
        public int ApplicationId { get; set; }

        public int ParentId { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }
    }
}
