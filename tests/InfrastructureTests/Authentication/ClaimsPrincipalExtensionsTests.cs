using System.Security.Claims;
using Infrastructure.Authentication;

namespace InfrastructureTests.Authentication;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetPictureUrl_Should_ReturnUrl_When_NamespacedPictureClaimPresent()
    {
        // Arrange
        const string pictureUrl = "https://example.com/avatar.jpg";
        var principal = CreatePrincipal(new Claim(Auth0ClaimTypes.Picture, pictureUrl));

        // Act
        var result = principal.GetPictureUrl();

        // Assert
        Assert.Equal(pictureUrl, result);
    }

    [Fact]
    public void GetPictureUrl_Should_ReturnNull_When_PictureClaimMissing()
    {
        // Arrange
        var principal = CreatePrincipal(new Claim(Auth0ClaimTypes.Email, "user@example.com"));

        // Act
        var result = principal.GetPictureUrl();

        // Assert
        Assert.Null(result);
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Bearer"));
}
