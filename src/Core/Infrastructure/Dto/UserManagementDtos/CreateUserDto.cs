using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Dto.UserManagementDtos
{
    public class CreateUserDto
    {
        [StringLength(450)]
        public string UserId { get; set; }

        [EmailAddress]
        [Required]
        [DisplayName("Email")]
        public string EmailAddress { get; set; }

        [MaxLength(64)]
        [MinLength(8)]
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [MaxLength(64)]
        [MinLength(8)]
        [Required]
        [DataType(DataType.Password)]
        [Compare("Password")]
        [DisplayName("Confirm Password")]
        public string ConfirmPassword { get; set; }

        [MaxLength(64)]
        [MinLength(4)]
        [Required]
        public string FirstName { get; set; }

        [MaxLength(64)]
        [MinLength(4)]
        [Required]
        public string LastName { get; set; }

        [DisplayName("Birth Date")]
        public DateTime BirthDate { get; set; }

        [MaxLength(16)]
        [MinLength(11)]
        [Required]
        [DataType(DataType.PhoneNumber)]
        [DisplayName("Phone Number")]
        public string PhoneNumber { get; set; }

        [MaxLength(256)]
        [MinLength(16)]
        public string BusinessAddress { get; set; }

        [MaxLength(256)]
        [MinLength(16)]
        public string HomeAddress { get; set; }

        public bool IsAdminUser { get; set; }

        public bool IsApprove { get; set; }
    }
}