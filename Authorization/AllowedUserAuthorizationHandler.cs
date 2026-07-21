using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace PSScriptWebApp.Authorization;

public class AllowedUserAuthorizationHandler : AuthorizationHandler<AllowedUserRequirement>
{
    private readonly IOptionsMonitor<AllowedUsersOptions> _options;

    public AllowedUserAuthorizationHandler(IOptionsMonitor<AllowedUsersOptions> options)
    {
        _options = options;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AllowedUserRequirement requirement)
    {
        var userName = context.User.Identity?.Name;

        if (!string.IsNullOrEmpty(userName) &&
            _options.CurrentValue.AllowedUsers.Any(u => string.Equals(u, userName, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
