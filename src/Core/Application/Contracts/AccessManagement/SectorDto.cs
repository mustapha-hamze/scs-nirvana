using System.ComponentModel.DataAnnotations;
using Domains.Entities;

namespace Application.Contracts.AccessManagement
{
    public class SectorDto : BaseEntity
    {
        public int ApplicationId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }
    }
}
