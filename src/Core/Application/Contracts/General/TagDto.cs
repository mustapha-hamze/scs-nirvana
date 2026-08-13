using System.ComponentModel.DataAnnotations;
using Domains.Entities;

namespace Application.Contracts.General
{
    public class TagDto : BaseEntity
    {
        public int ApplicationId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }
        public int TypeId { get; set; }
    }
}
