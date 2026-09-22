using System.ComponentModel.DataAnnotations;

namespace Web.Models.UserManagement
{
    public class UserLoginDto
    {

        [EmailAddress]
        [Required]
        public string EmailAddress { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [MaxLength(64)]
        public string Password { get; set; }

        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; }
    }
}