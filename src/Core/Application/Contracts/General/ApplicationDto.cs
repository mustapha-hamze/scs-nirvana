using System.ComponentModel.DataAnnotations;
using Domains.Entities;

namespace Application.Contracts.General
{
    public class ApplicationDto : BaseEntity
    {
        [Required]
        [MaxLength(64)]
        [MinLength(4)]
        public string Title { get; set; }

        [Required]
        [MaxLength(512)]
        [MinLength(32)]
        public string Description { get; set; }

        public string LogoFileName { get; set; }

        public string ApplicationKey { get; set; }
    }
}
