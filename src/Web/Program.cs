using System.Text.Json.Serialization;
using Infrastructure.AccessManagerRepository;
using Infrastructure.CMSRepository;
using Infrastructure.GeneralRepository;
using Infrastructure.Mapper;
using Infrastructure.Repository;
using Infrastructure.SCMRepository;
using Infrastructure.UserManagementRepository;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Web.Areas.BackOffice.Controllers;
using StackExchange.Redis;
using Microsoft.AspNetCore.Http.Features;

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
builder.Services.AddTransient<IEntityAccessServices, EntityAccessServices>();

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

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("RedisConnectionString")));

var mapperConfig = new MapperConfiguration(mc => mc.AddProfile(new MapperProfile()));
IMapper mapper = mapperConfig.CreateMapper();
builder.Services.AddSingleton(mapper);

builder.Services.AddHttpContextAccessor();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "WEBAPI.FRONT", Version = "v1" });
    // To Enable authorization using Swagger (JWT)    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your valid token in the text input below.\r\n\r\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9\"",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
              {
                  Reference = new OpenApiReference
                  {
                      Type = ReferenceType.SecurityScheme,
                      Id = "Bearer"
                  }
              },
              Array.Empty<string>()
        }
    });
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options => options.IdleTimeout = TimeSpan.FromDays(1));
builder.Services.AddMvc();
builder.Services.AddControllersWithViews().AddJsonOptions(
    options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    }
);
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.LoginPath = "/Login";
    options.SlidingExpiration = true;
});

builder.Services.AddAuthentication().AddCookie().AddGoogle(options =>
    {
        options.ClientId = "337798672568-eckdec0jp9j3sgihlvaqa6n80ebl80nu.apps.googleusercontent.com";
        options.ClientSecret = "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";
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

//app.UseMigrationsEndPoint();
//dbContext.Database.Migrate();

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

// app.UseSwagger();
// app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "WEBSITE.ADMIN v1"));

app.Run();
