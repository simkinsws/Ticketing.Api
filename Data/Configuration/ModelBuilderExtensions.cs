using Microsoft.EntityFrameworkCore;
using Ticketing.Api.Domain;

namespace Ticketing.Api.Data.Configuration;

public static class ModelBuilderExtensions
{
    public static ModelBuilder ConfigureTicket(this ModelBuilder builder)
    {
        builder.Entity<Ticket>(b =>
        {
            b.HasIndex(t => t.CustomerId);
            b.Property(t => t.Title).HasMaxLength(200).IsRequired();
            b.Property(t => t.Category).HasMaxLength(80).IsRequired();
            b.Property(t => t.Description).HasMaxLength(4000).IsRequired();

            b.HasOne(t => t.Customer)
                .WithMany()
                .HasForeignKey(t => t.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(t => t.AssignedAdmin)
                .WithMany()
                .HasForeignKey(t => t.AssignedAdminId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        return builder;
    }

    public static ModelBuilder ConfigureTicketComment(this ModelBuilder builder)
    {
        builder.Entity<TicketComment>(b =>
        {
            b.Property(c => c.Message).HasMaxLength(2000).IsRequired();

            b.HasOne(c => c.Ticket)
                .WithMany(t => t.Comments)
                .HasForeignKey(c => c.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        return builder;
    }

    public static ModelBuilder ConfigureRefreshToken(this ModelBuilder builder)
    {
        builder.Entity<RefreshToken>(b =>
        {
            b.HasIndex(r => new { r.UserId, r.TokenHash }).IsUnique();
            b.Property(r => r.TokenHash).HasMaxLength(64).IsRequired();
        });

        return builder;
    }
}
