using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Chat.App.Models;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Chat.App.Data;

/// <summary>
/// Database context for the enhanced chat application
/// </summary>
public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options) { }

    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<ChatMessage> Messages { get; set; }
    public DbSet<ConversationContext> Contexts { get; set; }
    public DbSet<MessageAttachment> Attachments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Conversation entity
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(450); // Match foreign key references
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Metadata)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
                );
            
            entity.HasMany(c => c.Messages)
                .WithOne()
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.UpdatedAt);
        });

        // Configure ChatMessage entity
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.ConversationId).HasMaxLength(450).IsRequired(); // Match Conversation.Id length
            entity.Property(e => e.Role).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.Metadata)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
                );
            entity.Property(e => e.Tools)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
                );
            entity.Property(e => e.ToolResult)
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => v == null ? null : JsonSerializer.Deserialize<ToolExecutionResult>(v, (JsonSerializerOptions?)null)
                );

            entity.HasMany(m => m.Attachments)
                .WithOne()
                .HasForeignKey(a => a.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Role);
        });

        // Configure ConversationContext entity
        modelBuilder.Entity<ConversationContext>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConversationId).HasMaxLength(450).IsRequired(); // Match Conversation.Id length
            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Data)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
                );
            entity.Property(e => e.Tags)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
                );

            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.LastAccessedAt);
        });

        // Configure MessageAttachment entity
        modelBuilder.Entity<MessageAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageId).HasMaxLength(450).IsRequired(); // Match ChatMessage.Id length
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.StoragePath).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Type).HasConversion<string>();
            entity.Property(e => e.Metadata)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>()
                );

            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => e.UploadedAt);
            entity.HasIndex(e => e.Type);
        });
    }
}