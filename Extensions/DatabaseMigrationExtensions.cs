using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;

namespace Ticketing.Api.Extensions;

public static class DatabaseMigrationExtensions
{
    public static async Task ApplyMigrationsAsync(this WebApplication app, IWebHostEnvironment environment)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            if (environment.IsDevelopment())
            {
                logger.LogInformation("Applying pending migrations in Development environment...");
                await db.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully.");
            }
            // Treat missing AutoApplyMigrations configuration as false (manual migrations by default in non-development environments).
            else if (app.Configuration.GetValue<bool>("AutoApplyMigrations", false))
            {
                logger.LogWarning("Applying pending migrations in Production environment. Ensure database backups exist.");
                await db.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully in Production.");
            }
            else
            {
                logger.LogInformation("AutoApplyMigrations is disabled in Production. Migrations must be applied manually during deployment.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying migrations. The application may not function correctly.");

            if (environment.IsDevelopment())
            {
                throw;
            }

            logger.LogCritical("Migration failed in Production. Application startup aborted. Please apply migrations manually and restart the application.");
            throw;
        }
    }
}
