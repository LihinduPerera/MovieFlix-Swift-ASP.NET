using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MovieFlix.Api.Data;
using MovieFlix.Api.Models;

namespace MovieFlix.Api.Data
{
    public static class DatabaseSeeder
    {
        public static async Task InitializeAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<MovieFlixDbContext>();
            var roleManager = scopedServices
                .GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scopedServices
                .GetRequiredService<UserManager<ApplicationUser>>();
            var timeProvider = scopedServices.GetRequiredService<TimeProvider>();

            // Apply pending migrations automatically (safe for SQLite in-memory
            // in tests as the connection is shared for the test factory).
            await db.Database.MigrateAsync();

            // Seed roles.
            await EnsureRoleAsync(roleManager, "Admin");
            await EnsureRoleAsync(roleManager, "User");

            // Optionally seed a default admin user from configuration.
            var config = scopedServices.GetRequiredService<IConfiguration>();
            var adminEmail = config["Seed:AdminEmail"];
            var adminPassword = config["Seed:AdminPassword"];
            var adminDisplayName = config["Seed:AdminDisplayName"] ?? "Admin";

            if (!string.IsNullOrWhiteSpace(adminEmail)
                && !string.IsNullOrWhiteSpace(adminPassword))
            {
                await EnsureAdminAsync(
                    userManager,
                    adminEmail,
                    adminPassword,
                    adminDisplayName,
                    timeProvider);
            }
        }

        private static async Task EnsureRoleAsync(
            RoleManager<IdentityRole> roleManager,
            string role)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        private static async Task EnsureAdminAsync(
            UserManager<ApplicationUser> userManager,
            string email,
            string password,
            string displayName,
            TimeProvider timeProvider)
        {
            var normalized = email.Trim().ToLowerInvariant();
            var existing = await userManager.FindByEmailAsync(normalized);

            if (existing is not null)
            {
                // Ensure user remains in Admin role.
                if (!await userManager.IsInRoleAsync(existing, "Admin"))
                {
                    await userManager.AddToRoleAsync(existing, "Admin");
                }

                return;
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var user = new ApplicationUser
            {
                UserName = normalized,
                Email = normalized,
                NormalizedEmail = normalized.ToUpperInvariant(),
                NormalizedUserName = normalized.ToUpperInvariant(),
                DisplayName = displayName.Trim(),
                EmailConfirmed = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            var result = await userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "Admin");
                await userManager.AddToRoleAsync(user, "User");
            }
        }
    }
}
