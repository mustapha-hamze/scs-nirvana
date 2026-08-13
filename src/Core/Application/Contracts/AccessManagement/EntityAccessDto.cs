using System.ComponentModel.DataAnnotations;
using Domains.Entities;

namespace Application.Contracts.AccessManagement
{
    public class EntityAccessDto : BaseEntity
    {
        // public int ApplicationId { get; set; }
        public int EntityId { get; set; }

        [StringLength(1024)]
        public string Access { get; set; }
    }
}
