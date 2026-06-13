using System.Security.Claims;
using Infrastructure.Authentication;
using Microsoft.AspNetCore.Http;

namespace InfrastructureTests.Authentication;

public class UserIdentityAccessorTests
{
    [Fact]
    public void UserIdentityAccessor_Should_ReturnPictureUrl_When_NamespacedClaimPresent()
    {
        // Arrange
        const string pictureUrl = "https://example.com/avatar.jpg";
        var httpContext = CreateHttpContext(new Claim(Auth0ClaimTypes.Picture, pictureUrl));
        var accessor = new UserIdentityAccessor(new HttpContextAccessor { HttpContext = httpContext });

        // Act
        var result = accessor.ProfilePictureUrl;

        // Assert
        Assert.Equal(pictureUrl, result);
    }

    [Fact]
    public void UserIdentityAccessor_Should_ReturnNull_When_ClaimMissing()
    {
        // Arrange
        var httpContext = CreateHttpContext(new Claim(Auth0ClaimTypes.Email, "user@example.com"));
        var accessor = new UserIdentityAccessor(new HttpContextAccessor { HttpContext = httpContext });

        // Act
        var result = accessor.ProfilePictureUrl;

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void UserIdentityAccessor_Should_ReturnNull_When_NotAuthenticated()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var accessor = new UserIdentityAccessor(new HttpContextAccessor { HttpContext = httpContext });

        // Act
        var result = accessor.ProfilePictureUrl;

        // Assert
        Assert.Null(result);
    }

    private static DefaultHttpContext CreateHttpContext(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Bearer"))
        };

        return httpContext;
    }
}
