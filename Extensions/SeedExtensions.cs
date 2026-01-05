using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Data;
using Ticketing.Api.Domain;

namespace Ticketing.Api.Extensions;

public static class SeedExtensions
{
    //Only used in development environment
    public static async Task SeedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var db = services.GetRequiredService<AppDbContext>();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { "Customer", "Admin" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var seedSection = app.Configuration.GetSection("Seed");
        var adminEmail = seedSection["AdminEmail"];
        var adminPassword = seedSection["AdminPassword"];

        ApplicationUser? admin = null;
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin is null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    DisplayName = "Admin"
                };

                var res = await userManager.CreateAsync(admin, adminPassword);
                if (res.Succeeded)
                    await userManager.AddToRoleAsync(admin, "Admin");
            }
            else
            {
                if (!await userManager.IsInRoleAsync(admin, "Admin"))
                    await userManager.AddToRoleAsync(admin, "Admin");
            }
        }

        // Seed sample customers and tickets if they don't exist
        await SeedSampleDataAsync(db, userManager, admin);
    }

    private static async Task SeedSampleDataAsync(AppDbContext db, UserManager<ApplicationUser> userManager, ApplicationUser? admin)
    {
        // Create sample customers if none exist
        if (!await db.Users.AnyAsync(u => u.Email == "customer1@local.test"))
        {
            var customers = new[]
            {
                new ApplicationUser
                {
                    UserName = "customer1@local.test",
                    Email = "customer1@local.test",
                    EmailConfirmed = true,
                    DisplayName = "John Smith"
                },
                new ApplicationUser
                {
                    UserName = "customer2@local.test",
                    Email = "customer2@local.test",
                    EmailConfirmed = true,
                    DisplayName = "Sarah Johnson"
                },
                new ApplicationUser
                {
                    UserName = "customer3@local.test",
                    Email = "customer3@local.test",
                    EmailConfirmed = true,
                    DisplayName = "Mike Davis"
                }
            };

            foreach (var customer in customers)
            {
                var res = await userManager.CreateAsync(customer, "Customer123$");
                if (res.Succeeded)
                    await userManager.AddToRoleAsync(customer, "Customer");
            }
        }

        // Create sample tickets if none exist
        if (!await db.Tickets.AnyAsync())
        {
            var customers = await db.Users
                .Where(u => u.Email!.StartsWith("customer"))
                .ToListAsync();

            if (customers.Any() && admin != null)
            {
                var tickets = new[]
                {
                    new Ticket
                    {
                        Title = "Login page not working",
                        Description = "I cannot log into the application. The page shows an error after entering my credentials.",
                        Category = "Bug",
                        Status = TicketStatus.Open,
                        Priority = TicketPriority.High,
                        CustomerId = customers[0].Id,
                        AssignedAdminId = admin.Id,
                        CreatedAt = DateTimeOffset.UtcNow.AddDays(-5)
                    },
                    new Ticket
                    {
                        Title = "Feature request: Dark mode",
                        Description = "Would love to have a dark mode option for the application.",
                        Category = "Feature Request",
                        Status = TicketStatus.Open,
                        Priority = TicketPriority.Low,
                        CustomerId = customers[1].Id,
                        AssignedAdminId = null,
                        CreatedAt = DateTimeOffset.UtcNow.AddDays(-3)
                    },
                    new Ticket
                    {
                        Title = "Database performance issue",
                        Description = "The application is slow when loading large datasets. Queries take too long.",
                        Category = "Performance",
                        Status = TicketStatus.InProgress,
                        Priority = TicketPriority.High,
                        CustomerId = customers[2].Id,
                        AssignedAdminId = admin.Id,
                        CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
                    },
                    new Ticket
                    {
                        Title = "Export to PDF functionality",
                        Description = "Need ability to export tickets to PDF format for reporting.",
                        Category = "Feature Request",
                        Status = TicketStatus.Resolved,
                        Priority = TicketPriority.Medium,
                        CustomerId = customers[0].Id,
                        AssignedAdminId = admin.Id,
                        CreatedAt = DateTimeOffset.UtcNow.AddDays(-10)
                    },
                    new Ticket
                    {
                        Title = "Typo in welcome message",
                        Description = "There's a spelling error in the welcome message on the dashboard.",
                        Category = "Bug",
                        Status = TicketStatus.Closed,
                        Priority = TicketPriority.Low,
                        CustomerId = customers[1].Id,
                        AssignedAdminId = admin.Id,
                        CreatedAt = DateTimeOffset.UtcNow.AddDays(-15)
                    }
                };

                await db.Tickets.AddRangeAsync(tickets);
                await db.SaveChangesAsync();

                // Add sample comments to tickets
                var ticketsWithComments = await db.Tickets.ToListAsync();
                if (ticketsWithComments.Any())
                {
                    var comments = new[]
                    {
                        new TicketComment
                        {
                            Message = "We are investigating this issue. Please provide more details about your browser and OS.",
                            TicketId = ticketsWithComments[0].Id,
                            CreatedAt = DateTimeOffset.UtcNow.AddDays(-4),
                            AuthorId = admin!.Id
                        },
                        new TicketComment
                        {
                            Message = "Dark mode is in our roadmap. We will implement it in the next major release.",
                            TicketId = ticketsWithComments[1].Id,
                            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
                            AuthorId = admin!.Id
                        },
                        new TicketComment
                        {
                            Message = "Thank you for reporting this. We've optimized the database queries.",
                            TicketId = ticketsWithComments[2].Id,
                            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                            AuthorId = admin!.Id
                        }
                    };

                    await db.TicketComments.AddRangeAsync(comments);
                    await db.SaveChangesAsync();
                }
            }
        }
    }
}
