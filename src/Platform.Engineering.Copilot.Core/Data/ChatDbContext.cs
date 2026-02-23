using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data;

/// <summary>
/// Chat DbContext for conversational history.
/// Isolated from platform data for independent scaling and purge (FR-074).
/// </summary>
public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options)
        : base(options) { }

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ConversationContext> ConversationContexts => Set<ConversationContext>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Conversation ──
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.ConversationId);

            entity.HasQueryFilter(e => !e.IsArchived);
        });

        // ── ChatMessage ──
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId);

            entity.HasIndex(e => new { e.ConversationId, e.Timestamp })
                .HasDatabaseName("IX_Message_ConversationId_Timestamp");

            entity.HasOne(e => e.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Role).HasConversion<string>();
        });

        // ── ConversationContext ──
        modelBuilder.Entity<ConversationContext>(entity =>
        {
            entity.HasKey(e => e.ContextId);

            entity.HasOne(e => e.Conversation)
                .WithMany(c => c.ContextEntries)
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
