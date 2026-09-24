using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Web.Models.UserManagement;
using Xunit;

namespace Web.Tests;

// UserLoginDto.Password intentionally carries no minimum length - legacy accounts created under
// the old 6-character Identity policy must still be able to log in (see
// ServiceCollectionExtensions.AddWebInfrastructure, which now requires 12 characters for
// new/changed passwords only). It does carry the same [MaxLength(64)] as the create/change
// password DTO, purely to bound the input the login endpoint accepts.
public sealed class UserLoginDtoValidationTests
{
    private static UserLoginDto MakeDto(string password) => new()
    {
        EmailAddress = "user@test.local",
        Password = password
    };

    private static bool IsValid(UserLoginDto dto, out List<ValidationResult> results)
    {
        results = new List<ValidationResult>();
        return Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
    }

    [Fact]
    public void SevenCharacterPassword_PassesValidation()
    {
        var dto = MakeDto(new string('a', 7));

        Assert.True(IsValid(dto, out var results), string.Join(", ", results));
    }

    [Fact]
    public void SixtyFiveCharacterPassword_FailsValidation()
    {
        var dto = MakeDto(new string('a', 65));

        Assert.False(IsValid(dto, out var results));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UserLoginDto.Password)));
    }

    [Fact]
    public void SixtyFourCharacterPassword_PassesValidation()
    {
        var dto = MakeDto(new string('a', 64));

        Assert.True(IsValid(dto, out var results), string.Join(", ", results));
    }

    [Fact]
    public void EmptyPassword_FailsValidation_BecauseRequired()
    {
        var dto = MakeDto(string.Empty);

        Assert.False(IsValid(dto, out var results));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UserLoginDto.Password)));
    }
}
