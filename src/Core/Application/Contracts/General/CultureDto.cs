using System.ComponentModel.DataAnnotations;
using Domains.Entities;

namespace Application.Contracts.General
{
    public class CultureDto : BaseEntity
    {
        public CultureDto()
        {
        }
        // public int ApplicationId { get; set; }
        [StringLength(64)]
        public string Title { get; set; }

        [StringLength(8)]
        public string Key { get; set; }
    }
}
