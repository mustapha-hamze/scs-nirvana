using Microsoft.AspNetCore.Identity;
using System;
using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Identity;

// The ASP.NET Identity user type. Kept in Infrastructure (not Domain) since it's coupled to
// IdentityUser, IdentityDbContext<ApplicationUser>, UserManager<ApplicationUser> and
// SignInManager<ApplicationUser> — all framework/persistence concerns.
public class ApplicationUser : IdentityUser
{
    public ApplicationUser()
    {

    }
    //property
    [StringLength(64)]
    public string FirstName { get; set; }

    [StringLength(64)]
    public string LastName { get; set; }

    public DateTime BirthDate { get; set; }

    // [StringLength(8192)]
    // public string Accesses { get; set; }

    [StringLength(256)]
    public string BusinessAddress { get; set; }

    [StringLength(256)]
    public string HomeAddress { get; set; }

    public bool IsAdminUser { get; set; }

    public bool IsApprove { get; set; }

    public int CurrentApplicationId { get; set; }

    public bool IsFrontEndUser { get; set; }

    public DateTime CreatedDT { get; set; }
    public DateTime UpdatedDT { get; set; }
}
