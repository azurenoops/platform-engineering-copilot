using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Core.Data;

namespace Platform.Engineering.Copilot.Tests.Integration;

/// <summary>
/// Factory for creating in-memory SQLite DbContext instances for integration testing.
/// Each call returns a fresh context with a unique in-memory database.
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Create a new PlatformEngineeringCopilotContext backed by in-memory SQLite.
    /// The database is created with schema applied via EnsureCreated().
    /// </summary>
    public static PlatformEngineeringCopilotContext CreatePlatformContext(string? databaseName = null)
    {
        var dbName = databaseName ?? $"TestDb_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<PlatformEngineeringCopilotContext>()
            .UseSqlite($"DataSource=:memory:")
            .Options;

        var context = new PlatformEngineeringCopilotContext(options);

        // SQLite in-memory databases are per-connection; keep the connection open
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        return context;
    }

    /// <summary>
    /// Create a new ChatDbContext backed by in-memory SQLite.
    /// </summary>
    public static ChatDbContext CreateChatContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseSqlite($"DataSource=:memory:")
            .Options;

        var context = new ChatDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        return context;
    }
}
