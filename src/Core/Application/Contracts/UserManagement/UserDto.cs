using System;

namespace Application.Contracts.UserManagement
{
    public class UserDto
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public DateTime BirthDate { get; set; }
        public DateTime CreatedDT { get; set; }
        public DateTime UpdatedDT { get; set; }
        public string Accesses { get; set; }
        public string PhoneNumber { get; set; }
        public string BusinessAddress { get; set; }
        public string HomeAddress { get; set; }
        public bool IsAdminUser { get; set; }
        public bool IsApprove { get; set; }
        public int CurrentApplicationId { get; set; }
    }
}
