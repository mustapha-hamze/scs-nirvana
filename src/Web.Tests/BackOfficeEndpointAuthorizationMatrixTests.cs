using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using Application.Contracts.AccessManagement;
using Application.Contracts.CMS;
using Application.Contracts.General;
using Application.Contracts.UserManagement;
using Application.UseCases.Utilities.ApplicationConst;
using Domains.Entities.CustomModule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Web.Areas.BackOffice.Controllers;
using Web.Areas.BackOffice.Features.Content.Contracts;
using Web.Areas.BackOffice.Models;
using Web.Authorization;
using Web.Filters;
using Web.Models.UserManagement;
using Xunit;

namespace Web.Tests;

// Web Phase 3 task 4: the endpoint audit. One row per conventional BackOffice/Api/root action -
// its intended HTTP verb(s), authentication level, tenant requirement/explicit opt-out, and
// permission/SuperAdmin rule - checked against the actual declared attributes (not just that
// *something* is there). Two data-dependent ContentController actions (ContentForm,
// SaveContentForm) aren't in this matrix because their required key depends on request data
// (create vs. edit) rather than a static attribute - see ContentController.cs's
// DenyIfMissingAccessAsync and AccessKeyAuthorizationTests' ContentForm_*/SaveContentForm_* cases
// for their dedicated behavioral coverage.
//
// Web Phase 4 task 1: AllPublicActionMethods_AreClassifiedExactlyOnceInTheMatrix below closes the
// regression gap the hand-maintained Matrix otherwise has - a new/renamed/overloaded action never
// added here previously went unaudited. It discovers every real MVC action (via the same
// IActionDescriptorCollectionProvider ASP.NET Core routing itself uses) under the audited root,
// BackOffice and Api controllers, and fails if one has no Matrix row, an unclassified overload, or
// the Matrix has more than one row for it.
//
// Reflection alone proves the attribute is declared, not that ASP.NET Core's authorization
// pipeline actually honors it the way the row claims - AuthorizationBehaviorTests pairs a
// representative row from every Auth kind with a real IAuthorizationService evaluation, and
// AccessKeyAuthorizationTests/CategorySchemaAccessKeyHttpTests below prove the AccessKey and
// SuperAdmin rows end-to-end over real HTTP for the features that weren't already covered.
public sealed class BackOfficeEndpointAuthorizationMatrixTests
{
    public enum Auth
    {
        Anonymous,
        Authenticated,
        SuperAdminRole,
        SuperAdminPolicy,
        AccessKey,
    }

    public sealed record Row(
        Type Controller,
        string Method,
        Type[] ParamTypes,
        string[] Verbs,
        Auth Auth,
        string[]? Keys = null,
        bool SkipTenant = false);

    private static Type[] P(params Type[] types) => types;
    private static string[] V(params string[] verbs) => verbs;
    private static string[] K(params string[] keys) => keys;

    private static readonly Row[] Matrix =
    {
        // ---- AccountController ----
        new(typeof(AccountController), nameof(AccountController.Users), P(), V("GET"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.Roles), P(), V("GET"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.Roles), P(typeof(string)), V("POST"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.AddUserToRole), P(typeof(string), typeof(string)), V("POST"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.RemoveUserFromRole), P(typeof(string), typeof(string)), V("POST"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.AddUserToApplication), P(typeof(string), typeof(int)), V("POST"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.RemoveUserFromApplication), P(typeof(int)), V("POST"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.UserSettingForm), P(typeof(string)), V("GET"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.Sectors), P(typeof(string)), V("GET"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.Entities), P(typeof(string)), V("GET"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.GetApplicationSectors), P(typeof(int)), V("GET"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.GetUserAccess), P(typeof(string), typeof(int)), V("GET"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.GetSectorEntities), P(typeof(int)), V("GET"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.SetAccessForUser), P(typeof(SaveAccessViewModel)), V("POST"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.UserForm), P(typeof(string)), V("GET"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.SaveUserForm), P(typeof(CreateUserDto)), V("POST"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.UserList), P(typeof(UserDto)), V("POST"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.Login), P(), V("GET"), Auth.Anonymous, SkipTenant: true),
        new(typeof(AccountController), nameof(AccountController.Login), P(typeof(UserLoginDto)), V("POST"), Auth.Anonymous, SkipTenant: true),
        new(typeof(AccountController), nameof(AccountController.Logout), P(), V("POST"), Auth.Authenticated, SkipTenant: true),
        new(typeof(AccountController), nameof(AccountController.EntityAccesses), P(typeof(int)), V("GET"), Auth.SuperAdminRole),
        new(typeof(AccountController), nameof(AccountController.Attachment), P(typeof(int)), V("GET"), Auth.SuperAdminPolicy),

        // ---- ApplicationController (whole controller skips the tenant filter - it's either
        // selecting/administering the tenant root itself, never consuming a selection) ----
        new(typeof(ApplicationController), nameof(ApplicationController.SelectApp), P(), V("GET"), Auth.Authenticated, SkipTenant: true),
        new(typeof(ApplicationController), nameof(ApplicationController.ApplicationForm), P(), V("GET"), Auth.Authenticated, SkipTenant: true),
        new(typeof(ApplicationController), nameof(ApplicationController.SaveApplicationForm), P(typeof(ApplicationDto)), V("POST"), Auth.SuperAdminRole, SkipTenant: true),
        new(typeof(ApplicationController), nameof(ApplicationController.UploadApplicationLogo), P(typeof(IFormFile), typeof(int)), V("POST"), Auth.SuperAdminRole, SkipTenant: true),
        new(typeof(ApplicationController), nameof(ApplicationController.SelectAppToEnter), P(typeof(int)), V("POST"), Auth.Authenticated, SkipTenant: true),
        new(typeof(ApplicationController), nameof(ApplicationController.WaitingForApproval), P(), V("GET"), Auth.Authenticated, SkipTenant: true),
        new(typeof(ApplicationController), nameof(ApplicationController.Logout), P(), V("POST"), Auth.Authenticated, SkipTenant: true),

        // ---- UserAttachmentController ----
        new(typeof(UserAttachmentController), nameof(UserAttachmentController.UserAttachmentForm), P(typeof(string), typeof(int)), V("GET"), Auth.SuperAdminRole),
        new(typeof(UserAttachmentController), nameof(UserAttachmentController.UserAttachmentsList), P(typeof(string)), V("GET"), Auth.SuperAdminRole),
        new(typeof(UserAttachmentController), nameof(UserAttachmentController.SaveUserAttachment), P(typeof(UserAttachmentDto)), V("POST"), Auth.SuperAdminRole),
        new(typeof(UserAttachmentController), nameof(UserAttachmentController.UploadAttachmentFile), P(typeof(IFormFile), typeof(string), typeof(int), typeof(string)), V("POST"), Auth.SuperAdminRole),

        // ---- CategoryController (one module key, declared at class level) ----
        new(typeof(CategoryController), nameof(CategoryController.Index), P(), V("GET"), Auth.AccessKey, K(AccessKeys.Category.Module)),
        new(typeof(CategoryController), nameof(CategoryController.Form), P(), V("GET"), Auth.AccessKey, K(AccessKeys.Category.Module)),
        new(typeof(CategoryController), nameof(CategoryController.SaveForm), P(typeof(CategoryDto)), V("POST"), Auth.AccessKey, K(AccessKeys.Category.Module)),
        new(typeof(CategoryController), nameof(CategoryController.List), P(), V("GET"), Auth.AccessKey, K(AccessKeys.Category.Module)),

        // ---- SchemaController (one module key, declared at class level) ----
        new(typeof(SchemaController), nameof(SchemaController.Index), P(), V("GET"), Auth.AccessKey, K(AccessKeys.Schema.Module)),
        new(typeof(SchemaController), nameof(SchemaController.SchemaForm), P(typeof(int)), V("GET"), Auth.AccessKey, K(AccessKeys.Schema.Module)),
        new(typeof(SchemaController), nameof(SchemaController.SaveSchemaForm), P(typeof(SchemaDto)), V("POST"), Auth.AccessKey, K(AccessKeys.Schema.Module)),
        new(typeof(SchemaController), nameof(SchemaController.UploadSchemaLogo), P(typeof(IFormFile), typeof(int)), V("POST"), Auth.AccessKey, K(AccessKeys.Schema.Module)),
        new(typeof(SchemaController), nameof(SchemaController.SchemaList), P(), V("GET"), Auth.AccessKey, K(AccessKeys.Schema.Module)),
        new(typeof(SchemaController), nameof(SchemaController.DeleteSchema), P(typeof(int)), V("DELETE"), Auth.AccessKey, K(AccessKeys.Schema.Module)),
        new(typeof(SchemaController), nameof(SchemaController.SchemaDetailsForm), P(typeof(int)), V("GET"), Auth.AccessKey, K(AccessKeys.Schema.Module)),
        new(typeof(SchemaController), nameof(SchemaController.SchemaDetailsList), P(typeof(int)), V("GET"), Auth.AccessKey, K(AccessKeys.Schema.Module)),
        new(typeof(SchemaController), nameof(SchemaController.SchemaDetailsFormSave), P(typeof(SchemaDetailsDto)), V("POST"), Auth.AccessKey, K(AccessKeys.Schema.Module)),

        // ---- SliderController (per-action keys) ----
        new(typeof(SliderController), nameof(SliderController.Index), P(), V("GET"), Auth.AccessKey, K(AccessKeys.Slider.Module)),
        new(typeof(SliderController), nameof(SliderController.List), P(), V("GET"), Auth.AccessKey, K(AccessKeys.Slider.Module)),
        new(typeof(SliderController), nameof(SliderController.Create), P(), V("GET"), Auth.AccessKey, K(AccessKeys.Slider.Add)),
        new(typeof(SliderController), nameof(SliderController.Create), P(typeof(Slider)), V("POST"), Auth.AccessKey, K(AccessKeys.Slider.Save)),
        new(typeof(SliderController), nameof(SliderController.CreateItem), P(typeof(int)), V("GET"), Auth.AccessKey, K(AccessKeys.Slider.AccessItems)),
        new(typeof(SliderController), nameof(SliderController.CreateItem), P(typeof(SliderItem)), V("POST"), Auth.AccessKey, K(AccessKeys.Slider.SaveItem)),
        new(typeof(SliderController), nameof(SliderController.UploadSliderItemImage), P(typeof(IFormFile), typeof(int), typeof(string)), V("POST"), Auth.AccessKey, K(AccessKeys.Slider.SaveItem, AccessKeys.Slider.UpdateItem)),
        new(typeof(SliderController), nameof(SliderController.SliderItems), P(), V("GET"), Auth.AccessKey, K(AccessKeys.Slider.AccessItems)),
        new(typeof(SliderController), nameof(SliderController.GetSliderItemList), P(typeof(int)), V("GET"), Auth.AccessKey, K(AccessKeys.Slider.AccessItems)),
        new(typeof(SliderController), nameof(SliderController.GetSliderItemForm), P(typeof(int), typeof(int)), V("GET"), Auth.AccessKey, K(AccessKeys.Slider.AccessItems)),
        new(typeof(SliderController), nameof(SliderController.UpdateItem), P(typeof(SliderItem)), V("POST"), Auth.AccessKey, K(AccessKeys.Slider.UpdateItem)),
        new(typeof(SliderController), nameof(SliderController.ActiveItem), P(typeof(int)), V("POST"), Auth.AccessKey, K(AccessKeys.Slider.Activity)),
        new(typeof(SliderController), nameof(SliderController.DeactiveItem), P(typeof(int)), V("POST"), Auth.AccessKey, K(AccessKeys.Slider.Activity)),
        new(typeof(SliderController), nameof(SliderController.DeleteItem), P(typeof(int)), V("DELETE"), Auth.AccessKey, K(AccessKeys.Slider.DeleteItem)),

        // ---- AccessManagementController (whole controller is SuperAdmin, per the
        // SuperAdmin-only "General Settings" sidebar section) ----
        new(typeof(AccessManagementController), nameof(AccessManagementController.Sectors), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(AccessManagementController), nameof(AccessManagementController.SectorForm), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(AccessManagementController), nameof(AccessManagementController.SectorList), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(AccessManagementController), nameof(AccessManagementController.SaveSector), P(typeof(SectorDto)), V("POST"), Auth.SuperAdminPolicy),
        new(typeof(AccessManagementController), nameof(AccessManagementController.EntityForm), P(typeof(int)), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(AccessManagementController), nameof(AccessManagementController.EntityList), P(typeof(int)), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(AccessManagementController), nameof(AccessManagementController.SaveEntity), P(typeof(SectorEntityDto)), V("POST"), Auth.SuperAdminPolicy),
        new(typeof(AccessManagementController), nameof(AccessManagementController.Accesses), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(AccessManagementController), nameof(AccessManagementController.AccessForm), P(typeof(int)), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(AccessManagementController), nameof(AccessManagementController.GetSectorEntities), P(typeof(int)), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(AccessManagementController), nameof(AccessManagementController.AccessList), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(AccessManagementController), nameof(AccessManagementController.SaveAccess), P(typeof(EntityAccessDto)), V("POST"), Auth.SuperAdminPolicy),

        // ---- GeneralController (whole controller is SuperAdmin, same sidebar section) ----
        new(typeof(GeneralController), nameof(GeneralController.Tags), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.TagForm), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.SaveTagForm), P(typeof(TagDto)), V("POST"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.TagList), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.Cultures), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.CultureForm), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.SaveCultureForm), P(typeof(CultureDto)), V("POST"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.CultureList), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.Logs), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.ApplicationSetting), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.ApplicationSettingForm), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.ApplicationSettingForm), P(typeof(ApplicationSettingDto)), V("POST"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.ApplicationSettingList), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.SystemTypes), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.SystemTypeForm), P(), V("GET"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.SaveSystemTypeForm), P(typeof(SystemTypeDto)), V("POST"), Auth.SuperAdminPolicy),
        new(typeof(GeneralController), nameof(GeneralController.SystemTypesList), P(), V("GET"), Auth.SuperAdminPolicy),

        // ---- HomeController (BackOffice) ----
        new(typeof(HomeController), nameof(HomeController.Index), P(), V("GET"), Auth.Authenticated),
        new(typeof(HomeController), nameof(HomeController.Error), P(), V(), Auth.Authenticated), // verb-unconstrained: exception re-execution must support the original verb

        // ---- ContentController (per-action keys; ContentForm/SaveContentForm excluded - see class doc) ----
        new(typeof(ContentController), nameof(ContentController.Index), P(typeof(int)), V("GET"), Auth.AccessKey, K(AccessKeys.Content.Module)),
        new(typeof(ContentController), nameof(ContentController.ContentList), P(typeof(int)), V("GET"), Auth.AccessKey, K(AccessKeys.Content.Module)),
        new(typeof(ContentController), nameof(ContentController.ChangeContentActiveMode), P(typeof(int), typeof(int), typeof(bool)), V("POST"), Auth.AccessKey, K(AccessKeys.Content.ChangeActivity)),
        new(typeof(ContentController), nameof(ContentController.DeleteContent), P(typeof(int)), V("DELETE"), Auth.AccessKey, K(AccessKeys.Content.Delete)),
        new(typeof(ContentController), nameof(ContentController.FarsiContentForm), P(typeof(int), typeof(int)), V("GET"), Auth.AccessKey, K(AccessKeys.Content.EditFarsi)),
        new(typeof(ContentController), nameof(ContentController.SaveFarsiContentForm), P(typeof(FarsiContentEditDto)), V("POST"), Auth.AccessKey, K(AccessKeys.Content.EditFarsi)),
        new(typeof(ContentController), nameof(ContentController.ContentRelations), P(typeof(int)), V("GET"), Auth.AccessKey, K(AccessKeys.Content.PreviewRelations)),
        new(typeof(ContentController), nameof(ContentController.ContentMetadata), P(typeof(int)), V("GET"), Auth.AccessKey, K(AccessKeys.Content.PreviewMetadata)),
        new(typeof(ContentController), nameof(ContentController.SaveRelation), P(typeof(string), typeof(string), typeof(int)), V("POST"), Auth.AccessKey, K(AccessKeys.Content.SaveRelations)),
        new(typeof(ContentController), nameof(ContentController.SaveContentMetadata), P(typeof(ContentMetadataDto)), V("POST"), Auth.AccessKey, K(AccessKeys.Content.SaveMetadata)),
        new(typeof(ContentController), nameof(ContentController.ContentSections), P(typeof(int), typeof(int)), V("GET"), Auth.AccessKey, K(AccessKeys.Content.PreviewBody)),
        new(typeof(ContentController), nameof(ContentController.CreateContentSection), P(typeof(int), typeof(int)), V("GET"), Auth.AccessKey, K(AccessKeys.Content.SaveBody)),
        new(typeof(ContentController), nameof(ContentController.SaveSection), P(typeof(SaveContentBodyDto)), V("POST"), Auth.AccessKey, K(AccessKeys.Content.SaveBody)),
        new(typeof(ContentController), nameof(ContentController.UpdateSectionsLayoutOrder), P(typeof(string)), V("POST"), Auth.AccessKey, K(AccessKeys.Content.SaveBody)),
        new(typeof(ContentController), nameof(ContentController.DeleteSection), P(typeof(int)), V("DELETE"), Auth.AccessKey, K(AccessKeys.Content.SaveBody)),
        new(typeof(ContentController), nameof(ContentController.ContentImages), P(typeof(int)), V("GET"), Auth.AccessKey, K(AccessKeys.Content.PreviewImages)),
        new(typeof(ContentController), nameof(ContentController.UploadBodyImage), P(typeof(IFormFile)), V("POST"), Auth.AccessKey, K(AccessKeys.Content.SaveBody)),
        new(typeof(ContentController), nameof(ContentController.UploadBodyFile), P(typeof(IFormFile), typeof(string)), V("POST"), Auth.AccessKey, K(AccessKeys.Content.SaveBody)),
        new(typeof(ContentController), nameof(ContentController.UploadBodyImageGallery), P(), V("POST"), Auth.AccessKey, K(AccessKeys.Content.SaveBody)),
        new(typeof(ContentController), nameof(ContentController.UploadContentImage), P(typeof(IFormFile), typeof(int), typeof(int)), V("POST"), Auth.AccessKey, K(AccessKeys.Content.UploadImages)),

        // ---- Root HomeController (never area-scoped; anonymous by design) ----
        new(typeof(Web.Controllers.HomeController), nameof(Web.Controllers.HomeController.Index), P(), V("GET"), Auth.Anonymous),
        new(typeof(Web.Controllers.HomeController), nameof(Web.Controllers.HomeController.Error), P(), V(), Auth.Anonymous),

        // ---- Public Api controllers (class-level [AllowAnonymous]) ----
        new(typeof(Web.Areas.Api.CategoryController), nameof(Web.Areas.Api.CategoryController.GetCategories), P(typeof(int), typeof(int)), V("GET"), Auth.Anonymous),
        new(typeof(Web.Areas.Api.ContentController), nameof(Web.Areas.Api.ContentController.Get), P(typeof(int), typeof(int), typeof(CancellationToken)), V("GET"), Auth.Anonymous),
        new(typeof(Web.Areas.Api.ContentController), nameof(Web.Areas.Api.ContentController.GetContentByTypeId), P(typeof(int), typeof(int), typeof(CancellationToken)), V("GET"), Auth.Anonymous),
        new(typeof(Web.Areas.Api.ContentController), nameof(Web.Areas.Api.ContentController.GetContentByTypeId), P(typeof(int), typeof(int), typeof(int), typeof(CancellationToken)), V("GET"), Auth.Anonymous),
        new(typeof(Web.Areas.Api.ContentController), nameof(Web.Areas.Api.ContentController.GetContentByCategoryId), P(typeof(int), typeof(int), typeof(int), typeof(int), typeof(CancellationToken)), V("GET"), Auth.Anonymous),
        new(typeof(Web.Areas.Api.ContentController), nameof(Web.Areas.Api.ContentController.GetContentByCategoryIdByDate), P(typeof(int), typeof(int), typeof(DateTime), typeof(DateTime), typeof(int), typeof(CancellationToken)), V("GET"), Auth.Anonymous),
        new(typeof(Web.Areas.Api.ContentController), nameof(Web.Areas.Api.ContentController.GetContentInCategoryAsBox), P(typeof(int), typeof(int), typeof(CancellationToken)), V("GET"), Auth.Anonymous),
        new(typeof(Web.Areas.Api.SliderController), nameof(Web.Areas.Api.SliderController.GetSlider), P(typeof(int), typeof(int)), V("GET"), Auth.Anonymous),
    };

    public static TheoryData<Row> MatrixData()
    {
        var data = new TheoryData<Row>();
        foreach (var row in Matrix)
            data.Add(row);
        return data;
    }

    private static readonly string[] VerbAttributeNames =
        { "HttpGetAttribute", "HttpPostAttribute", "HttpPutAttribute", "HttpDeleteAttribute", "HttpPatchAttribute" };

    [Theory]
    [MemberData(nameof(MatrixData))]
    public void Endpoint_MatchesDeclaredVerbAuthTenantAndPermission(Row row)
    {
        var method = row.Controller.GetMethod(row.Method,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, row.ParamTypes);
        Assert.True(method is not null,
            $"{row.Controller.Name}.{row.Method}({string.Join(",", row.ParamTypes.Select(t => t.Name))}) not found.");

        // Verb: the exact set of HttpMethodAttribute-derived attributes present (from either a
        // bare [HttpGet] or a combined [HttpGet("route")]) must match the expected set.
        var declaredVerbs = method!.GetCustomAttributes(inherit: false)
            .Where(a => VerbAttributeNames.Contains(a.GetType().Name))
            .SelectMany(a => ((Microsoft.AspNetCore.Mvc.Routing.IActionHttpMethodProvider)a).HttpMethods)
            .Distinct()
            .OrderBy(v => v)
            .ToArray();
        Assert.Equal(row.Verbs.OrderBy(v => v), declaredVerbs);

        // Tenant opt-out: [SkipTenantContextCheck] on the method or the controller.
        var skipsTenant = method.GetCustomAttribute<SkipTenantContextCheckAttribute>(inherit: true) is not null
            || row.Controller.GetCustomAttribute<SkipTenantContextCheckAttribute>(inherit: true) is not null;
        Assert.Equal(row.SkipTenant, skipsTenant);

        var methodAuthorize = method.GetCustomAttributes<AuthorizeAttribute>(inherit: false).ToArray();
        var classAuthorize = row.Controller.GetCustomAttributes<AuthorizeAttribute>(inherit: false).ToArray();
        var isAllowAnonymous = method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: false) is not null
            || row.Controller.GetCustomAttribute<AllowAnonymousAttribute>(inherit: false) is not null;
        var methodRequireAccess = method.GetCustomAttribute<RequireAccessAttribute>(inherit: false);
        var classRequireAccess = row.Controller.GetCustomAttribute<RequireAccessAttribute>(inherit: false);
        var requireAccess = methodRequireAccess ?? classRequireAccess;

        switch (row.Auth)
        {
            case Auth.Anonymous:
                Assert.True(isAllowAnonymous, $"{row.Controller.Name}.{row.Method} must carry [AllowAnonymous].");
                break;

            case Auth.Authenticated:
                Assert.False(isAllowAnonymous, $"{row.Controller.Name}.{row.Method} must not be anonymous.");
                Assert.DoesNotContain(methodAuthorize, a => !string.IsNullOrEmpty(a.Roles) || !string.IsNullOrEmpty(a.Policy));
                Assert.Null(requireAccess);
                break;

            case Auth.SuperAdminRole:
                Assert.False(isAllowAnonymous);
                Assert.Contains(methodAuthorize, a => a.Roles == ApplicationRoles.SuperAdmin);
                Assert.Null(requireAccess);
                break;

            case Auth.SuperAdminPolicy:
                Assert.False(isAllowAnonymous);
                var hasPolicy = methodAuthorize.Concat(classAuthorize).Any(a => a.Policy == WebAuthorizationPolicies.SuperAdmin);
                Assert.True(hasPolicy, $"{row.Controller.Name}.{row.Method} must carry [Authorize(Policy = WebAuthorizationPolicies.SuperAdmin)] (method or class).");
                Assert.Null(requireAccess);
                break;

            case Auth.AccessKey:
                Assert.False(isAllowAnonymous);
                Assert.NotNull(requireAccess);
                Assert.Equal(row.Keys!.OrderBy(k => k), requireAccess!.Keys.OrderBy(k => k));
                break;
        }
    }

    // Named, explicit dynamic-permission exceptions - see the class doc above and
    // AccessKeyAuthorizationTests' ContentForm_*/SaveContentForm_* cases.
    private static readonly (Type Controller, string Method)[] DynamicPermissionExceptions =
    {
        (typeof(ContentController), nameof(ContentController.ContentForm)),
        (typeof(ContentController), nameof(ContentController.SaveContentForm)),
    };

    private static string ActionKey(Type controller, string method, Type[] paramTypes) =>
        $"{controller.FullName}.{method}({string.Join(",", paramTypes.Select(t => t.FullName))})";

    [Fact]
    public void AllPublicActionMethods_AreClassifiedExactlyOnceInTheMatrix()
    {
        using var factory = new TestWebApplicationFactory();
        var actionDescriptors = factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>();

        var auditedNamespaces = new[] { "Web.Controllers", "Web.Areas.BackOffice.Controllers", "Web.Areas.Api" };
        var discovered = actionDescriptors
            .Where(a => auditedNamespaces.Contains(a.ControllerTypeInfo.Namespace))
            .Select(a => (Controller: a.ControllerTypeInfo.AsType(), a.MethodInfo))
            .Distinct()
            .ToArray();
        Assert.NotEmpty(discovered);

        var exceptionKeys = DynamicPermissionExceptions
            .Select(e => discovered.Where(d => d.Controller == e.Controller && d.MethodInfo.Name == e.Method)
                .Select(d => ActionKey(d.Controller, d.MethodInfo.Name, d.MethodInfo.GetParameters().Select(p => p.ParameterType).ToArray())))
            .SelectMany(k => k)
            .ToHashSet();

        var matrixKeys = Matrix.Select(r => ActionKey(r.Controller, r.Method, r.ParamTypes)).ToArray();
        var duplicateMatrixKeys = matrixKeys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
        Assert.True(duplicateMatrixKeys.Length == 0,
            "Matrix has duplicate row(s) for: " + string.Join(", ", duplicateMatrixKeys));

        var discoveredKeys = discovered
            .Select(d => ActionKey(d.Controller, d.MethodInfo.Name, d.MethodInfo.GetParameters().Select(p => p.ParameterType).ToArray()))
            .ToArray();

        var missingFromMatrix = discoveredKeys.Except(exceptionKeys).Except(matrixKeys).ToArray();
        Assert.True(missingFromMatrix.Length == 0,
            "Discovered action(s) with no Matrix row (missing or unclassified overload): " + string.Join(", ", missingFromMatrix));

        var staleMatrixRows = matrixKeys.Except(discoveredKeys).ToArray();
        Assert.True(staleMatrixRows.Length == 0,
            "Matrix row(s) referencing an action MVC does not route (stale/renamed): " + string.Join(", ", staleMatrixRows));
    }

    // ---- Public endpoint list: every action classified Auth.Anonymous above, restated as a
    // flat list so a change to that classification is visible as a diff here too. ----
    [Fact]
    public void PublicEndpoints_MatchTheAnonymousRowsInTheMatrix()
    {
        var anonymousRoutes = new[]
        {
            "GET /", // root Home.Index
            "* /Home/Error", // root Home.Error (verb-unconstrained; exception re-execution target)
            "GET,POST /Login", // AccountController.Login
            "GET /healthz", // mapped directly in Program.cs, outside the MVC pipeline - not part of this matrix
            "GET /api/Category/GetCategories/{applicationId}/{parent?}",
            "GET /api/Content/GetContent/{applicationId}/{id}",
            "GET /api/Content/GetContentByTypeId/{applicationId}/{typeId}",
            "GET /api/Content/GetContentByTypeId/{applicationId}/{typeId}/{pageIndex}",
            "GET /api/Content/GetContentByCategoryId/{applicationId}/{categoryId}/{pageIndex?}/{pageSize?}",
            "GET /api/Content/GetContentByCategoryIdByDate/{applicationId}/{categoryId}/{startDate}/{endDate}/{pageIndex?}",
            "GET /api/Content/GetContentInCategoryAsBox/{applicationId}/{categoryId}",
            "GET /api/Slider/{applicationId}/GetSlider/{sliderId}",
        };

        // healthz isn't a matrix row (mapped directly in Program.cs, never goes through this
        // controller-attribute model); Login's GET+POST are one route line above but two matrix
        // rows. So: 12 route lines - 1 (healthz) + 1 (Login's extra row) = 12 matrix rows.
        Assert.Equal(12, anonymousRoutes.Length - 1 + 1);
        Assert.Equal(12, Matrix.Count(r => r.Auth == Auth.Anonymous));
    }
}
