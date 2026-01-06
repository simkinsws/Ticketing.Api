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
            var autoApplyMigrations = app.Configuration.GetValue<bool>("AutoApplyMigrations", false);
            logger.LogInformation("Starting database migration check. Environment: {Environment}, AutoApplyMigrations: {AutoApplyMigrations}", environment.EnvironmentName, autoApplyMigrations);

            if (environment.IsDevelopment())
            {
                logger.LogInformation("Running in Development environment - Applying pending migrations...");
                await db.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully in Development environment.");
            }
            else if (autoApplyMigrations)
            {
                logger.LogWarning("Running in {Environment} environment with AutoApplyMigrations enabled - Applying pending migrations. Ensure database backups exist.", environment.EnvironmentName);
                await db.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully in {Environment} environment.", environment.EnvironmentName);
            }
            else
            {
                logger.LogInformation("AutoApplyMigrations is disabled in {Environment} environment. Migrations must be applied manually during deployment.", environment.EnvironmentName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying migrations. The application may not function correctly.");

            if (environment.IsDevelopment())
            {
                throw;
            }

            logger.LogCritical("Migration failed in {Environment} environment. The application may not function correctly until migrations are applied. Please apply migrations manually and restart the application if necessary. Error details: {ExceptionMessage}", environment.EnvironmentName, ex.Message);
        }
    }
}
