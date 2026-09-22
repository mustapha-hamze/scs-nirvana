using Web.Extensions;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddCmsServices();
builder.Services.AddWebInfrastructure();

// Every unhandled exception is logged here (server-side only) with its trace id; /api/... requests
// additionally get an RFC 7807 JSON body instead of falling through to the MVC HTML error page.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<Web.ApiExceptionHandler>();

var app = builder.Build();

// One-time super admin bootstrap, opt-in only. Disabled by default; enable by setting
// SuperAdminSeed:Enabled=true (e.g. via a local .env value) together with
// SuperAdminSeed:Email and SuperAdminSeed:Password, then remove/disable it again after
// the account has been created. No credentials are hardcoded here or reachable over HTTP.
if (app.Configuration.GetValue<bool>("SuperAdminSeed:Enabled"))
{
    using var seedScope = app.Services.CreateScope();
    var userManager = seedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = seedScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    var seedEmail = app.Configuration["SuperAdminSeed:Email"];
    var seedPassword = app.Configuration["SuperAdminSeed:Password"];

    if (!string.IsNullOrWhiteSpace(seedEmail) && !string.IsNullOrWhiteSpace(seedPassword)
        && await userManager.FindByEmailAsync(seedEmail) is null)
    {
        var superAdmin = new ApplicationUser
        {
            FirstName = "Admin",
            LastName = "Tech",
            Email = seedEmail,
            UserName = seedEmail,
            IsAdminUser = true,
            CreatedDT = DateTime.Now,
            UpdatedDT = DateTime.Now,
            IsApprove = true
        };

        var createResult = await userManager.CreateAsync(superAdmin, seedPassword);
        if (createResult.Succeeded)
        {
            if (!await roleManager.RoleExistsAsync("SuperAdmin"))
            {
                await roleManager.CreateAsync(new IdentityRole("SuperAdmin"));
            }

            await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

#pragma warning disable ASP0014
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "BackOffice",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
    endpoints.MapControllerRoute(
        name: "Api",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}");
    endpoints.MapRazorPages();
});

app.Run();

// Exposes the implicit top-level Program class to WebApplicationFactory<Program> in Web.Tests.
public partial class Program { }
