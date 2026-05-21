using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using QuotesApi.Models;

namespace QuotesApi.Authorization;

public class OwnQuoteHandler : AuthorizationHandler<OwnQuoteRequirement, Quote>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnQuoteRequirement requirement,
        Quote resource)
    {
        // Prefer the standard NameIdentifier claim; fall back to "sub" as issued by JwtSecurityToken.
        var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (sub is not null
            && int.TryParse(sub, out var userId)
            && resource.OwnerId == userId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
