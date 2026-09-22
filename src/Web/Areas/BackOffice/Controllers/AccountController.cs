namespace Web.Areas.BackOffice.Controllers;

[Authorize]

[Area("BackOffice")]
[Route("/BackOffice/{controller}/{action}")]
public class AccountController : BaseController
{
    //: fields
    #region fields
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountController> _logger;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IUserManagementServices _userManagementServices;
    private readonly ICurrentApplicationContext _currentApplicationContext;
    private readonly IApplicationServices _applicationServices;
    private readonly ISectorServices _sectorServices;
    private readonly ISectorEntityServices _SectorEntityServices;
    private readonly IEntityAccessServices _entityAccessServices;
    #endregion

    //: constructor
    #region constructor
    public AccountController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
        SignInManager<ApplicationUser> signInManager, ILogger<AccountController> logger,
        IUserManagementServices userManagementServices, ICurrentApplicationContext currentApplicationContext,
        IApplicationServices applicationServices,
        ISectorServices sectorServices, ISectorEntityServices SectorEntityServices,
        IEntityAccessServices entityAccessServices)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _roleManager = roleManager;
        _userManagementServices = userManagementServices;
        _currentApplicationContext = currentApplicationContext;
        _applicationServices = applicationServices;
        _sectorServices = sectorServices;
        _SectorEntityServices = SectorEntityServices;
        _entityAccessServices = entityAccessServices;
    }
    #endregion

    //: methods
    #region methods

    // User administration (listing every user, editing another user's profile, or flipping
    // IsAdminUser/IsApprove) is privileged, global administration - same rationale as role and
    // membership administration above.
    [Authorize(Roles = "SuperAdmin")]
    public IActionResult Users()
    {
        return View();
    }

    // Role administration - creating a role, or granting/revoking one (including SuperAdmin
    // itself) - is privileged, global (not tenant-scoped) administration. Authentication plus a
    // selected-tenant membership must never be enough; the "User Management" sidebar section
    // already hides this from non-SuperAdmins client-side, this is what actually enforces it.
    [Authorize(Roles = "SuperAdmin")]
    public IActionResult Roles()
    {
        var roles = _roleManager.Roles.ToList();
        return View(roles);
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    public async Task<IActionResult> Roles(string RoleName)
    {
        await _roleManager.CreateAsync(new IdentityRole { Name = RoleName });
        return Redirect("/BackOffice/Account/Roles");
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    [Route("/{area}/Account/AddUserToRole/{userId}/{roleName}")]
    public async Task<IActionResult> AddUserToRole(string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        var result = await _userManager.AddToRoleAsync(user, roleName);
        if (result.Succeeded)
            return Content("Done");
        else
            return Content("Failed");
    }

    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    [Route("/{area}/Account/RemoveUserFromRole/{userId}/{roleName}")]
    public async Task<IActionResult> RemoveUserFromRole(string userId, string roleName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        var result = await _userManager.RemoveFromRoleAsync(user, roleName);
        if (result.Succeeded)
            return Content("Done");
        else
            return Content("Failed");
    }

    // Membership administration is privileged, global administration, same as role
    // administration above: applicationId here is an explicit, SuperAdmin-only target, never
    // proof of authorization by itself (an ordinary member could otherwise add/remove anyone
    // to/from any application, tenant membership notwithstanding).
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    [Route("/{area}/Account/AddUserToApplication/{userId}/{applicationId}")]
    public async Task<IActionResult> AddUserToApplication(string userId, int applicationId)
    {
        await _applicationServices.AddUserToApplication(userId, applicationId);
        return Content("Done");
    }

    // applicationId comes from the caller's own validated selected tenant, not the request -
    // a SuperAdmin using this action can only remove membership rows for the application they
    // themselves currently have selected.
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    [Route("/{area}/Account/RemoveUserFromApplication/{relationId}")]
    public async Task<IActionResult> RemoveUserFromApplication(int relationId)
    {
        await _applicationServices.RemoveUserFromApplication(relationId, _currentApplicationContext.RequireApplicationId());
        return Content("Done");
    }

    [Authorize(Roles = "SuperAdmin")]
    [Route("/{area}/Account/UserSettingForm/{userId}")]
    public async Task<IActionResult> UserSettingForm(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        ViewData["UserId"] = userId;

        ViewData["Roles"] = _roleManager.Roles.ToList();
        ViewData["Applications"] = _applicationServices.List();
        ViewData["UserRoles"] = await _userManager.GetRolesAsync(user);
        ViewData["UserApplications"] = await _applicationServices.GetUserApplications(user.UserName);

        return View();
    }

    // Cross-application by design (see ISectorEntityServices.GetSectorEntities(sectorId)): an
    // admin here is deliberately working across every application a user belongs to, not just
    // the caller's own selected tenant, so this must stay a global, SuperAdmin-only flow rather
    // than being bound to the selected tenant.
    [Authorize(Roles = "SuperAdmin")]
    [Route("/BackOffice/Account/Sectors/{userId}")]
    public async Task<IActionResult> Sectors(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        ViewData["Applications"] = _applicationServices.List();
        ViewData["UserApplications"] = await _applicationServices.GetUserApplications(user.UserName);
        ViewData["Sectors"] = _sectorServices.GetAllSector();

        return View();
    }

    [Authorize(Roles = "SuperAdmin")]
    [Route("/{area}/{controller}/Entities/{userId}")]
    public async Task<IActionResult> Entities(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        ViewData["Applications"] = _applicationServices.List();
        ViewData["UserApplications"] = await _applicationServices.GetUserApplications(user.UserName);

        return View();
    }

    // appId is caller-supplied and unrelated to the caller's own selected tenant - without this
    // gate any authenticated member could enumerate another application's sectors by guessing
    // appId, regardless of their own membership.
    [Authorize(Roles = "SuperAdmin")]
    [Route("/{area}/{controller}/GetApplicationSectors/{appId}")]
    public IActionResult GetApplicationSectors(int appId)
    {
        var sectors = _sectorServices.GetAllSector(appId);
        return PartialView("_SectorOptionsPartial", sectors);
    }

    [Authorize(Roles = "SuperAdmin")]
    [Route("/{area}/{controller}/{action}/{userId}/{appId}")]
    public async Task<string> GetUserAccess(string userId, int appId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        var accesses = await _userManagementServices.GetUserAccesses(user.Email, appId);
        return accesses;
    }

    [Authorize(Roles = "SuperAdmin")]
    [Route("/{area}/{controller}/GetSectorEntities/{sectorId}")]
    public IActionResult GetSectorEntities(int sectorId)
    {
        var entities = _SectorEntityServices.GetSectorEntities(sectorId);
        return PartialView("_SectorEntityLinksPartial", entities);
    }

    [Authorize(Roles = "SuperAdmin")]
    [Route("/{area}/{controller}/{action}")]
    [HttpPost]
    public async Task<IActionResult> SetAccessForUser(SaveAccessViewModel model)
    {
        // var user = await _userManager.FindByIdAsync(model.UserId);
        // if (model.Accesses == "A")
        //     user.Accesses = "";
        // else
        //     user.Accesses = model.Accesses;

        // var result = await _userManager.UpdateAsync(user);

        await _userManagementServices.SetUserAccesses(model.Accesses, model.UserId, model.ApplicationId);

        // if (!result.Succeeded)
        //     return Content("Operation failed please try again");
        // else
        return Content("Done");
    }

    [Authorize(Roles = "SuperAdmin")]
    [Route("/{area}/Account/UserForm/{userId?}")]
    public async Task<IActionResult> UserForm(string userId = "")
    {
        if (userId != "")
        {
            var user = await _userManager.FindByIdAsync(userId);
            var _user = new CreateUserDto
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                BirthDate = user.BirthDate,
                BusinessAddress = user.BusinessAddress,
                EmailAddress = user.Email,
                HomeAddress = user.HomeAddress,
                IsAdminUser = user.IsAdminUser,
                PhoneNumber = user.PhoneNumber,
                UserId = user.Id,
                IsApprove = user.IsApprove
            };
            return View(_user);
        }
        else
        {
            return View();
        }
    }

    // CreateUserDto.IsAdminUser/IsApprove bind directly from the posted form with no further
    // check - without this gate, any authenticated member could self-approve, grant themselves
    // IsAdminUser, or edit another user's profile/approval state by posting a crafted DTO
    // (UserId selects create vs. update, and update trusts every field on the DTO).
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    public async Task<IActionResult> SaveUserForm(CreateUserDto user)
    {
        if (user.UserId != "")
        {
            ModelState.Remove("Password");
            ModelState.Remove("ConfirmPassword");
        }

        if (!ModelState.IsValid)
            return View("UserForm", user);

        if (String.IsNullOrEmpty(user.UserId))
        {
            var result = await _userManager.CreateAsync(new ApplicationUser
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                BirthDate = user.BirthDate,
                PhoneNumber = user.PhoneNumber,
                Email = user.EmailAddress,
                UserName = user.EmailAddress,
                BusinessAddress = user.BusinessAddress,
                HomeAddress = user.HomeAddress,
                IsAdminUser = user.IsAdminUser,
                CreatedDT = DateTime.Now,
                UpdatedDT = DateTime.Now,
                IsApprove = user.IsApprove
            }, user.Password);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("error", "Operation failed please try again");
                return View("UserForm", user);
            }

            return Content("Done");
        }
        else
        {
            var __user = await _userManager.FindByIdAsync(user.UserId);

            __user.FirstName = user.FirstName;
            __user.LastName = user.LastName;
            __user.BirthDate = user.BirthDate;
            __user.PhoneNumber = user.PhoneNumber;
            __user.UserName = user.EmailAddress;
            __user.BusinessAddress = user.BusinessAddress;
            __user.HomeAddress = user.HomeAddress;
            __user.IsAdminUser = user.IsAdminUser;
            __user.UpdatedDT = DateTime.Now;
            __user.IsApprove = user.IsApprove;

            var result = await _userManager.UpdateAsync(__user);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("error", "Operation failed please try again");
                return View("UserForm", user);
            }

            return Content("Done");
        }
    }

    // Enumerates every user in the system (optionally filtered) - global administration, not
    // tenant-scoped self-service.
    [Authorize(Roles = "SuperAdmin")]
    [HttpPost]
    public IActionResult UserList(UserDto userFilter)
    {
        if (string.IsNullOrEmpty(userFilter.Email))
            return Content("");

        return View(_userManagementServices.List(userFilter.IsAdminUser, userFilter.Email));
    }

    [AllowAnonymous]
    [SkipTenantContextCheck]
    [Route("/Login")]
    public IActionResult Login()
    {
        return View(new UserLoginDto());
    }

    [AllowAnonymous]
    [SkipTenantContextCheck]
    [Route("/Login")]
    [HttpPost]
    public async Task<IActionResult> Login(UserLoginDto userLogin)
    {
        if (!ModelState.IsValid)
            return View(userLogin);

        var user = await _userManager.FindByNameAsync(userLogin.EmailAddress);

        if (user == null)
        {
            ModelState.AddModelError("user_data_wrong", "Login information was wrong, please try again");
            return View(userLogin);
        }

        if (!user.IsAdminUser)
        {
            ModelState.AddModelError("user_data_wrong", "Login information was wrong, please try again");
            return View(userLogin);
        }

        var result = await _signInManager.PasswordSignInAsync(user, userLogin.Password, true, true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("user_data_wrong", "Login information was wrong, please try again");
            return View(userLogin);
        }

        CookieOptions option = new();
        option.Expires = DateTime.Now.AddDays(1);
        Response.Cookies.Append("UserIsApprove", "true", option);

        return Redirect("/BackOffice/Application/SelectApp");
    }

    [SkipTenantContextCheck]
    [Route("/Logout")]
    public async Task<IActionResult> Logout()
    {
        // HttpContext.Session.Remove("AppKey");
        Response.Cookies.Delete("AppKey");
        Response.Cookies.Delete("UserIsApprove");
        _currentApplicationContext.CurrentApplicationId = null;
        await _signInManager.SignOutAsync();
        return Redirect("/");
    }

    // id is an entity id with no applicationId scoping at all - same cross-tenant risk as
    // GetApplicationSectors above.
    [Authorize(Roles = "SuperAdmin")]
    [Route("/BackOffice/Account/EntityAccesses/{id}")]
    public IActionResult EntityAccesses(int id)
    {
        var entityAccesses = _entityAccessServices.GetEntityAccesses(id);
        return PartialView("_EntityAccessCheckboxesPartial", entityAccesses);
    }

    [Route("/BackOffice/Account/Attachment")]
    public IActionResult Attachment(int id)
    {
        return View();
    }
    #endregion
}