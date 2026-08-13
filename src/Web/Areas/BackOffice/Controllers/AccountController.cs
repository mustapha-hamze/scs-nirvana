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
    private readonly IApplicationServices _applicationServices;
    private readonly ISectorServices _sectorServices;
    private readonly ISectorEntityServices _SectorEntityServices;
    private readonly IEntityAccessServices _entityAccessServices;
    #endregion

    //: constructor
    #region constructor
    public AccountController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager,
        SignInManager<ApplicationUser> signInManager, ILogger<AccountController> logger,
        IUserManagementServices userManagementServices, IApplicationServices applicationServices,
        ISectorServices sectorServices, ISectorEntityServices SectorEntityServices,
        IEntityAccessServices entityAccessServices)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _roleManager = roleManager;
        _userManagementServices = userManagementServices;
        _applicationServices = applicationServices;
        _sectorServices = sectorServices;
        _SectorEntityServices = SectorEntityServices;
        _entityAccessServices = entityAccessServices;
    }
    #endregion

    //: methods
    #region methods

    public IActionResult Users()
    {
        return View();
    }

    public IActionResult Roles()
    {
        var roles = _roleManager.Roles.ToList();
        return View(roles);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Roles(string RoleName)
    {
        await _roleManager.CreateAsync(new IdentityRole { Name = RoleName });
        return Redirect("/BackOffice/Account/Roles");
    }

    [Route("/{area}/Account/AddUserToRole/{userId}/{roleName}")]
    public async Task<IActionResult> AddUserToRole(string userId, string roleName)
    {
        //TODO: Implement Realistic Implementation
        var user = await _userManager.FindByIdAsync(userId);
        var result = await _userManager.AddToRoleAsync(user, roleName);
        if (result.Succeeded)
            return Content("Done");
        else
            return Content("Failed");
    }

    [Route("/{area}/Account/RemoveUserFromRole/{userId}/{roleName}")]
    public async Task<IActionResult> RemoveUserFromRole(string userId, string roleName)
    {
        //TODO: Implement Realistic Implementation
        var user = await _userManager.FindByIdAsync(userId);
        var result = await _userManager.RemoveFromRoleAsync(user, roleName);
        if (result.Succeeded)
            return Content("Done");
        else
            return Content("Failed");
    }

    [Route("/{area}/Account/AddUserToApplication/{userId}/{applicationId}")]
    public async Task<IActionResult> AddUserToApplication(string userId, int applicationId)
    {
        //TODO: Implement Realistic Implementation
        await _applicationServices.AddUserToApplication(userId, applicationId);
        return Content("Done");
    }

    [Route("/{area}/Account/RemoveUserFromApplication/{relationId}")]
    public async Task<IActionResult> RemoveUserFromApplication(int relationId)
    {
        //TODO: Implement Realistic Implementation
        await _applicationServices.RemoveUserFromApplication(relationId);
        return Content("Done");
    }

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

    [Route("/BackOffice/Account/Sectors/{userId}")]
    public async Task<IActionResult> Sectors(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        ViewData["Applications"] = _applicationServices.List();
        ViewData["UserApplications"] = await _applicationServices.GetUserApplications(user.UserName);
        ViewData["Sectors"] = _sectorServices.GetAllSector();

        return View();
    }

    [Route("/{area}/{controller}/Entities/{userId}")]
    public async Task<IActionResult> Entities(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);

        ViewData["Applications"] = _applicationServices.List();
        ViewData["UserApplications"] = await _applicationServices.GetUserApplications(user.UserName);

        return View();
    }

    [Route("/{area}/{controller}/GetApplicationSectors/{appId}")]
    public string GetApplicationSectors(int appId)
    {
        string html = string.Empty;

        var sectors = _sectorServices.GetAllSector(appId);

        foreach (var item in sectors)
        {
            html += "<option value='" + item.Id + "'>" + item.Title + "</option>";
        }

        return html;
    }

    [Route("/{area}/{controller}/{action}/{userId}/{appId}")]
    public async Task<string> GetUserAccess(string userId, int appId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        var accesses = await _userManagementServices.GetUserAccesses(user.Email, appId);
        return accesses;
    }

    [Route("/{area}/{controller}/GetSectorEntities/{sectorId}")]
    public string GetSectorEntities(int sectorId)
    {
        string html = string.Empty;

        var entities = _SectorEntityServices.GetSectorEntities(sectorId);

        foreach (var item in entities)
        {
            html += "<div class=''>";
            html += "<a href='javascript:void(0);' onclick='loadEntityAccesses(\"" + item.AccessKey + "\", \"" + item.Id
                + "\")'>" + item.Title + "</a>";
            html += "</div>";
        }

        return html;
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

    [HttpPost]
    [ValidateAntiForgeryToken]
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
                ModelState.AddModelError("error", "Opertion failed please try again");
                return View("UserForm", user);
            }

            return Content("Done");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UserList(UserDto userFilter)
    {
        if (string.IsNullOrEmpty(userFilter.Email))
            return Content("");

        return View(_userManagementServices.List(userFilter.IsAdminUser, userFilter.Email));
    }

    [AllowAnonymous]
    [Route("/Login")]
    public async Task<IActionResult> Login()
    {
        var loginModel = new UserLoginDto
        {
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList()
        };
        return View(loginModel);
    }

    [AllowAnonymous]
    [Route("/{area}/ExternalLogin/{provider}")]
    public IActionResult ExternalLogin(string provider)
    {
        var redirectUrl = Url.Action("ExternalLoginCallBack", "Account");
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return new ChallengeResult(provider, properties);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallBack(string remoteError = null)
    {
        var loginModel = new UserLoginDto
        {
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList()
        };

        if (remoteError != null)
        {
            ModelState.AddModelError(string.Empty, $"Error from google login: {remoteError}");
            return View("Login", loginModel);
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            ModelState.AddModelError(string.Empty, "Error loading external login information.");
            return View("Login", loginModel);
        }

        var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider,
            info.ProviderKey, true, true);

        if (signInResult.Succeeded)
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var user = await _userManager.FindByEmailAsync(email);
            if (user.IsApprove)
            {
                CookieOptions option = new();
                option.Expires = DateTime.Now.AddDays(1);
                Response.Cookies.Append("UserIsApprove", "true", option);
                return Redirect("/BackOffice/Application/SelectApp");
            }
            else
            {
                CookieOptions option = new();
                option.Expires = DateTime.Now.AddDays(1);
                Response.Cookies.Append("UserIsApprove", "false", option);

                return Redirect("/WaitingForApproval");
            }
        }
        else
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);

            if (email != null)
            {
                var user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = info.Principal.FindFirstValue(ClaimTypes.Email),
                        Email = info.Principal.FindFirstValue(ClaimTypes.Email),
                        IsAdminUser = true,
                        IsApprove = false
                    };
                    await _userManager.CreateAsync(user);
                }

                await _userManager.AddLoginAsync(user, info);
                await _signInManager.SignInAsync(user, true);

                CookieOptions option = new();
                option.Expires = DateTime.Now.AddDays(1);
                Response.Cookies.Append("UserIsApprove", "false", option);

                return Redirect("/WaitingForApproval");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Google login isn't available, please contact support on info@entralon.com");
                return View("Login", loginModel);
            }
        }
    }

    [AllowAnonymous]
    [Route("/Login")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(UserLoginDto userLogin, string _password = "")
    {
        userLogin.ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
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

    [Route("/Logout")]
    public async Task<IActionResult> Logout()
    {
        // HttpContext.Session.Remove("AppKey");
        Response.Cookies.Delete("AppKey");
        Response.Cookies.Delete("UserIsApprove");
        await _signInManager.SignOutAsync();
        return Redirect("/");
    }

    [Route("/BackOffice/Account/EntityAccesses/{id}")]
    public string EntityAccesses(int id)
    {
        var entityAccesses = _entityAccessServices.GetEntityAccesses(id);

        string html = string.Empty;

        foreach (var item in entityAccesses)
        {
            html += "<div class='form-check form-checkbox-success'>";
            html += "<input type='checkbox' onchange='setEntityAccessHideInput(\"" + item.Access + "\")' id='"
                + item.Access + "' access='" + item.Access + "' class='form-check-input'>";
            html += "<a href='javascript:void(0);' onclick='loadEntityAccesses(\"" + item.Access + "\")'>" + item.Access + "</a>";
            html += "</div>";
            //html += "<option value='" + item.Id + "'>" + item.Title + "</option>";
        }

        return html;
    }

    [Route("/BackOffice/Account/Attachment")]
    public IActionResult Attachment(int id)
    {
        return View();
    }
    #endregion
}