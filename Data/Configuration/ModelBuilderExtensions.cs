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
            b.HasIndex(t => t.TicketNumber).IsUnique(); // Make ticket number unique and indexed
            
            b.Property(t => t.TicketNumber)
                .ValueGeneratedOnAdd() // Auto-increment
                .UseIdentityColumn(seed: 1, increment: 1); // Start at 1
            
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

    public static ModelBuilder ConfigureConversation(this ModelBuilder builder)
    {
        builder.Entity<Conversation>(b =>
        {
            b.ToTable("Conversations");
            b.HasKey(c => c.Id);

            b.Property(c => c.CustomerUserId).IsRequired().HasMaxLength(450);
            b.Property(c => c.CustomerDisplayName).IsRequired().HasMaxLength(200);
            b.Property(c => c.LastMessagePreview).IsRequired().HasMaxLength(500);
            b.Property(c => c.CreatedAt).IsRequired();
            b.Property(c => c.LastMessageAt).IsRequired();
            b.Property(c => c.UnreadForAdminCount).IsRequired();
            b.Property(c => c.UnreadForCustomerCount).IsRequired();
            b.Property(c => c.LastCustomerReadAt).IsRequired(false);
            b.Property(c => c.IsOpen).IsRequired();

            b.HasIndex(c => c.CustomerUserId);
            b.HasIndex(c => c.LastMessageAt);

            b.HasOne(c => c.Customer)
                .WithMany()
                .HasForeignKey(c => c.CustomerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        return builder;
    }

    public static ModelBuilder ConfigureMessage(this ModelBuilder builder)
    {
        builder.Entity<Message>(b =>
        {
            b.ToTable("Messages");
            b.HasKey(m => m.Id);

            b.Property(m => m.ConversationId).IsRequired();
            b.Property(m => m.SenderUserId).IsRequired().HasMaxLength(450);
            b.Property(m => m.Text).IsRequired().HasMaxLength(2000);
            b.Property(m => m.CreatedAt).IsRequired();

            b.HasIndex(m => new { m.ConversationId, m.CreatedAt });
            b.HasIndex(m => m.SenderUserId);

            b.HasOne(m => m.Conversation)
                .WithMany()
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        return builder;
    }

    public static ModelBuilder ConfigureUserPreferences(this ModelBuilder builder)
    {
        builder.Entity<UserPreferences>(b =>
        {
            b.ToTable("UserPreferences");
            
            // UserId is Primary Key (1-to-1 with ApplicationUser)
            b.HasKey(p => p.UserId);
            
            // Configure relationship
            b.HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<UserPreferences>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Properties
            b.Property(p => p.Timezone).HasMaxLength(100);
            b.Property(p => p.Language).HasMaxLength(10);
            b.Property(p => p.DateFormat).HasMaxLength(20);
            b.Property(p => p.TimeFormat).HasMaxLength(10);
        });

        return builder;
    }
}
