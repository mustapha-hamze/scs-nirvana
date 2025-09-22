using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.PropTechDtos
{
    public class LeadDto
    {
        [Required]
        [MaxLength(64)]
        [MinLength(3)]
        public string FullName { get; set; }

        [MinLength(9)]
        [MaxLength(16)]
        [Required]
        public string Phone { get; set; }

        [EmailAddress]
        [MaxLength(128)]
        [Required]
        public string EmailAddress { get; set; }

        [Required]
        public int Country { get; set; }

        [Required]
        public int DevelopmentId { get; set; }

        public bool HasError { get; set; }
    }
}