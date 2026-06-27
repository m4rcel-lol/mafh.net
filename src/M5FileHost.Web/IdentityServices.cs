using System.Security.Claims;
using M5FileHost.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace M5FileHost.Web;

public sealed class AppClaimsPrincipalFactory(UserManager<ApplicationUser> users, RoleManager<IdentityRole<Guid>> roles, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(users, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
        return identity;
    }
}

public static class OwnerSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["Owner:Email"];
        var username = configuration["Owner:Username"];
        var password = configuration["Owner:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return;
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (existing.Role != UserRole.Owner) { existing.Role = UserRole.Owner; await users.UpdateAsync(existing); }
            return;
        }
        var owner = new ApplicationUser { Id = Guid.NewGuid(), Email = email, UserName = username, DisplayName = username, EmailConfirmed = true, IsVerified = true, Role = UserRole.Owner };
        var result = await users.CreateAsync(owner, password);
        if (!result.Succeeded) throw new InvalidOperationException("Owner seeding failed: " + string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
