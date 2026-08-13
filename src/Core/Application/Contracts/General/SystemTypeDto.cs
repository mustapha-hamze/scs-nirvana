using System.ComponentModel.DataAnnotations;
using Domains.Entities;

namespace Application.Contracts.General
{
    public class SystemTypeDto : BaseEntity
    {
        public int ApplicationId { get; set; }

        public int TypeGroupId { get; set; }

        public bool IsRTL { get; set; }

        [StringLength(128)]
        public string Title { get; set; }
    }
}
