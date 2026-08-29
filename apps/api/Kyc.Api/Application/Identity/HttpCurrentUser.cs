using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Kyc.Api.Domain.Identity;

namespace Kyc.Api.Application.Identity;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public const string UserIdClaimType = JwtRegisteredClaimNames.Sub;
    public const string RoleClaimType = "role";

    public Guid? UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var raw = user.FindFirstValue(UserIdClaimType);
            return Guid.TryParse(raw, out var userId) ? userId : null;
        }
    }

    public UserRole? Role
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var raw = user.FindFirstValue(RoleClaimType);
            return Enum.TryParse<UserRole>(raw, ignoreCase: false, out var role) ? role : null;
        }
    }
}
