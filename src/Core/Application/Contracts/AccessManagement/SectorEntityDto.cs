using System.ComponentModel.DataAnnotations;
using Domains.Entities;

namespace Application.Contracts.AccessManagement
{
    public class SectorEntityDto : BaseEntity
    {
        public int SectorId { get; set; }

        [StringLength(64)]
        public string Title { get; set; }

        [StringLength(256)]
        public string AccessKey { get; set; }
    }
}
