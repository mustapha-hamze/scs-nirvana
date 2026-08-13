using System.Text.Json.Serialization;
using Application.AccessManagerRepository;
using Application.CMSRepository;
using Application.GeneralRepository;
using Application.Repository;
using Application.SCMRepository;
using Application.UserManagementRepository;
using Application.UnitOfWork;
using Infrastructure.AccessManagerRepository;
using Infrastructure.CMSRepository;
using Infrastructure.GeneralRepository;
using Infrastructure.Mapper;
using Infrastructure.Repository;
using Infrastructure.SCMRepository;
using Infrastructure.UserManagementRepository;
using Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Web.Areas.BackOffice.Controllers;
using Microsoft.AspNetCore.Http.Features;
using Application.ContentManagement;
using Infrastructure.ContentManagement;
using Newtonsoft.Json;
using Core.Services.TranslatorServices;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FormOptions>(options =>
    {
        options.ValueCountLimit = int.MaxValue;
        options.ValueLengthLimit = int.MaxValue;
        options.MultipartBodyLengthLimit = 60000000; // Change this value to the desired maximum size in bytes
    });

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IUserManagementRepository), typeof(UserManagementRepository));
builder.Services.AddTransient<IUserManagementServices, UserManagementServices>();
builder.Services.AddScoped(typeof(IApplicationRepository), typeof(ApplicationRepository));
builder.Services.AddTransient<IApplicationServices, ApplicationServices>();
builder.Services.AddScoped(typeof(ITagRepository), typeof(TagRepository));
builder.Services.AddTransient<ITagServices, TagServices>();
builder.Services.AddScoped(typeof(ICultureRepository), typeof(CultureRepository));
builder.Services.AddTransient<ICultureServices, CultureServices>();
builder.Services.AddScoped(typeof(ISchemaRepository), typeof(SchemaRepository));
builder.Services.AddTransient<ISchemaServices, SchemaServices>();
builder.Services.AddScoped(typeof(ICategoryRepository), typeof(CategoryRepository));
builder.Services.AddTransient<ICategoryServices, CategoryServices>();
builder.Services.AddScoped(typeof(IContentRepository), typeof(ContentRepository));
builder.Services.AddTransient<IContentServices, ContentServices>();
builder.Services.AddScoped(typeof(ISliderRepository), typeof(SliderRepository));
builder.Services.AddTransient<ISliderServices, SliderServices>();
builder.Services.AddScoped(typeof(ISystemTypeRepository), typeof(SystemTypeRepository));
builder.Services.AddTransient<ISystemTypeServices, SystemTypeServices>();
builder.Services.AddScoped(typeof(ISectorRepository), typeof(SectorRepository));
builder.Services.AddTransient<ISectorServices, SectorServices>();
builder.Services.AddScoped(typeof(ISectorEntityRepository), typeof(SectorEntityRepository));
builder.Services.AddTransient<ISectorEntityServices, SectorEntityServices>();
builder.Services.AddScoped(typeof(IEntityAccessRepository), typeof(EntityAccessRepository));
builder.Services.AddTransient<IEntityAccessServices, EntityAccessServices>();
builder.Services.AddTransient<IContentTranslator, ContentTranslator>();

builder.Services.AddScoped<BaseController>();

builder.Services.AddDefaultIdentity<ApplicationUser>(
    options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddMediatR(AppDomain.CurrentDomain.GetAssemblies());

var mapperConfig = new MapperConfiguration(mc => mc.AddProfile(new MapperProfile()));
IMapper mapper = mapperConfig.CreateMapper();
builder.Services.AddSingleton(mapper);

builder.Services.AddHttpContextAccessor();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options => options.IdleTimeout = TimeSpan.FromDays(1));
builder.Services.AddMvc();

builder.Services.AddTransient<IContentProvider, ContentProvider>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.LoginPath = "/Login";
    options.SlidingExpiration = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseDeveloperExceptionPage();
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

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
