using Microsoft.AspNetCore.Identity;
using System;

namespace Domains.Entities.User;

// Identity integration concern: this type is coupled to ASP.NET Core Identity (IdentityUser)
// rather than being a persistence-agnostic domain type. Kept here to avoid disrupting
// IdentityDbContext<ApplicationUser> and the many repositories/services that depend on it.
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

