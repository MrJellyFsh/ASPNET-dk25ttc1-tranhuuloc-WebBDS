using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NhaTot.Models;

namespace NhaTot.Data;

public static class IdentitySeedService
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try
        {
            if (!await dbContext.Database.CanConnectAsync())
            {
                logger.LogWarning("Identity seed was skipped because the configured SQL Server database is unavailable.");
                return;
            }
        }
        catch (SqlException)
        {
            logger.LogWarning("Identity seed was skipped because the configured SQL Server database is unavailable.");
            return;
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var roleName in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Unable to create required role '{roleName}': {string.Join("; ", result.Errors.Select(error => error.Description))}");
            }
        }

        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("No bootstrap administrator is configured. Roles were seeded only.");
            return;
        }
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Bootstrap administrator was not created because both BootstrapAdmin:Email and BootstrapAdmin:Password are required.");
            return;
        }

        var administrator = await userManager.FindByEmailAsync(email);
        if (administrator is null)
        {
            administrator = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, FullName = "System Administrator" };
            var result = await userManager.CreateAsync(administrator, password);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Unable to create bootstrap administrator: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
        if (!await userManager.IsInRoleAsync(administrator, RoleNames.Administrator))
        {
            var result = await userManager.AddToRoleAsync(administrator, RoleNames.Administrator);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Unable to assign bootstrap administrator role: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }
}
