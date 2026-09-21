using Application.AccessManagerRepository;
using Application.CMSRepository;
using Application.ContentManagement;
using Application.GeneralRepository;
using Application.SCMRepository;
using Application.UserManagementRepository;
using Application.UnitOfWork;
using Application.UseCases.TranslatorServices;
using Infrastructure.AccessManagerRepository;
using Infrastructure.CMSRepository;
using Infrastructure.ContentManagement;
using Infrastructure.GeneralRepository;
using Infrastructure.Mapper;
using Infrastructure.SCMRepository;
using Infrastructure.TranslatorServices;
using Infrastructure.UserManagementRepository;
using Infrastructure.UnitOfWork;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

namespace Web.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>Data access: the EF Core context, the unit of work, and every aggregate-specific repository.</summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // Injected into ApplicationDbContext so it can stamp CreatedDT/UpdatedDT centrally
        // (UTC) at SaveChanges time instead of every repository calling DateTime.Now by hand.
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));
        services.AddDatabaseDeveloperPageExceptionFilter();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IUserManagementRepository, UserManagementRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<ICultureRepository, CultureRepository>();
        services.AddScoped<ISchemaRepository, SchemaRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IContentQueryRepository, ContentQueryRepository>();
        services.AddScoped<IContentCommandRepository, ContentCommandRepository>();
        services.AddScoped<IContentRelationRepository, ContentRelationRepository>();
        services.AddScoped<IContentsInCategoryQueryAdapter, ContentsInCategoryQueryAdapter>();
        services.AddScoped<ISliderRepository, SliderRepository>();
        services.AddScoped<ISystemTypeRepository, SystemTypeRepository>();
        services.AddScoped<ISectorRepository, SectorRepository>();
        services.AddScoped<ISectorEntityRepository, SectorEntityRepository>();
        services.AddScoped<IEntityAccessRepository, EntityAccessRepository>();
        services.AddScoped<IUserAttachmentRepository, UserAttachmentRepository>();

        return services;
    }

    /// <summary>General-purpose (non-CMS) business services.</summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddTransient<IUserManagementServices, UserManagementServices>();
        services.AddTransient<IApplicationServices, ApplicationServices>();
        services.AddTransient<ITagServices, TagServices>();
        services.AddTransient<ICultureServices, CultureServices>();
        services.AddTransient<ISystemTypeServices, SystemTypeServices>();
        services.AddTransient<ISectorServices, SectorServices>();
        services.AddTransient<ISectorEntityServices, SectorEntityServices>();
        services.AddTransient<IEntityAccessServices, EntityAccessServices>();

        return services;
    }

    /// <summary>Content-management business services.</summary>
    public static IServiceCollection AddCmsServices(this IServiceCollection services)
    {
        services.AddTransient<ISchemaServices, SchemaServices>();
        services.AddTransient<ICategoryServices, CategoryServices>();
        services.AddTransient<IContentServices, ContentServices>();
        services.AddTransient<ISliderServices, SliderServices>();

        services.AddOptions<OpenAiTranslationOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                options.ApiKey = configuration["OPENAI_API_KEY"];
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddTransient<ITranslationPort, OpenAiTranslationPort>();
        services.AddTransient<IContentTranslator, ContentTranslator>();

        services.AddTransient<IContentProvider, ContentProvider>();

        return services;
    }

    /// <summary>Cross-cutting ASP.NET Core hosting concerns: identity, MVC, MediatR, mapping, sessions, uploads.</summary>
    public static IServiceCollection AddWebInfrastructure(this IServiceCollection services)
    {
        services.Configure<FormOptions>(options =>
        {
            options.ValueCountLimit = int.MaxValue;
            options.ValueLengthLimit = int.MaxValue;
            options.MultipartBodyLengthLimit = 60000000; // Change this value to the desired maximum size in bytes
        });

        services.AddDefaultIdentity<ApplicationUser>(options =>
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

        services.AddMediatR(typeof(IUnitOfWork).Assembly);

        services.AddAutoMapper(new[] { typeof(MapperProfile).Assembly, typeof(IUnitOfWork).Assembly }, ServiceLifetime.Singleton);

        services.AddHttpContextAccessor();

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
        });

        services.AddDistributedMemoryCache();
        services.AddSession(options => options.IdleTimeout = TimeSpan.FromDays(1));

        services.AddControllersWithViews();
        services.AddRazorPages();

        services.AddTransient<IFileUploadService, FileUploadService>();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.LoginPath = "/Login";
            options.SlidingExpiration = true;
        });

        return services;
    }
}
