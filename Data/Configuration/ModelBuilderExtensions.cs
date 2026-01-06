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

    public static ModelBuilder ConfigureNotification(this ModelBuilder builder)
    {
        builder.Entity<Notification>(b =>
        {
            b.ToTable("Notifications");

            b.HasKey(n => n.Id);

            b.HasIndex(n => n.UserId);
            b.HasIndex(n => n.TicketId);

            b.HasIndex(n => new { n.UserId, n.CreatedAtUtc });

            b.Property(n => n.UserId)
                .IsRequired()
                .HasMaxLength(450);

            b.Property(n => n.Title)
                .IsRequired()
                .HasMaxLength(200);

            b.Property(n => n.Message)
                .IsRequired()
                .HasMaxLength(2000);

            b.Property(n => n.CreatedAtUtc)
                .IsRequired();

            b.Property(n => n.ReadAtUtc)
                .IsRequired(false);

            b.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(n => n.Ticket)
                .WithMany()
                .HasForeignKey(n => n.TicketId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
        });

        return builder;
    }
}
