using Chatbot.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Chatbot.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ChatSession> Sessions => Set<ChatSession>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<StoredFile> Files => Set<StoredFile>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ChatSession>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.UpdatedAt });
            e.Property(x => x.Title).HasMaxLength(200);
            e.HasMany(x => x.Messages)
                .WithOne(x => x.Session!)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ChatMessage>(e =>
        {
            e.HasIndex(x => x.SessionId);
            e.Property(x => x.Content).HasColumnType("text");
            e.HasMany(x => x.Attachments)
                .WithOne(x => x.Message!)
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<StoredFile>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.Property(x => x.S3Key).HasMaxLength(1024);
        });
    }
}
