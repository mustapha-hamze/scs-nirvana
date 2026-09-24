using System.Reflection;
using Core.Tests.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Web.Areas.BackOffice.Controllers;
using Xunit;

namespace Core.Tests.Architecture;

// The whole UserAttachment BackOffice workflow (viewing, listing, creating, and uploading a
// file for another user's attachment) is privileged, global administration - same rationale as
// user/role/membership administration in AccountController. attachmentId/userId are caller
// supplied route/form values, never proof of authorization by themselves.
public class UserAttachmentAuthorizationTests
{
    private static readonly MethodInfo UserAttachmentForm = typeof(UserAttachmentController).GetMethod(nameof(UserAttachmentController.UserAttachmentForm));
    private static readonly MethodInfo UserAttachmentsList = typeof(UserAttachmentController).GetMethod(nameof(UserAttachmentController.UserAttachmentsList));
    private static readonly MethodInfo SaveUserAttachment = typeof(UserAttachmentController).GetMethod(nameof(UserAttachmentController.SaveUserAttachment));
    private static readonly MethodInfo UploadAttachmentFile = typeof(UserAttachmentController).GetMethod(nameof(UserAttachmentController.UploadAttachmentFile));

    public static IEnumerable<object[]> UserAttachmentActions() =>
        new[] { UserAttachmentForm, UserAttachmentsList, SaveUserAttachment, UploadAttachmentFile }.Select(m => new object[] { m });

    [Theory]
    [MemberData(nameof(UserAttachmentActions))]
    public async Task OrdinaryMember_IsDenied(MethodInfo method)
    {
        Assert.False(await AuthorizeRoleTestHelper.SatisfiesAsync(method, AuthorizeRoleTestHelper.OrdinaryMember()));
    }

    [Theory]
    [MemberData(nameof(UserAttachmentActions))]
    public async Task SuperAdmin_IsAllowed(MethodInfo method)
    {
        Assert.True(await AuthorizeRoleTestHelper.SatisfiesAsync(method, AuthorizeRoleTestHelper.SuperAdmin()));
    }

    [Fact]
    public void UserAttachmentActions_RequireSuperAdmin()
    {
        foreach (var method in new[] { UserAttachmentForm, UserAttachmentsList, SaveUserAttachment, UploadAttachmentFile })
        {
            var requiresSuperAdmin = method.GetCustomAttributes<AuthorizeAttribute>(inherit: false).Any(a => a.Roles == "SuperAdmin");
            Assert.True(requiresSuperAdmin, $"{method.Name} must carry [Authorize(Roles = \"SuperAdmin\")].");
        }
    }
}
