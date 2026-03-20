using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data;

/// <summary>
/// Main DbContext for all platform operational entities.
/// Infrastructure, remediation, audit, configuration data.
/// </summary>
public class PlatformEngineeringCopilotContext : DbContext
{
    public PlatformEngineeringCopilotContext(DbContextOptions<PlatformEngineeringCopilotContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Entities.Configuration> Configurations => Set<Entities.Configuration>();
    public DbSet<RemediationBoard> RemediationBoards => Set<RemediationBoard>();
    public DbSet<RemediationTask> RemediationTasks => Set<RemediationTask>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<IaCTemplate> IaCTemplates => Set<IaCTemplate>();
    public DbSet<ServiceTemplate> ServiceTemplates => Set<ServiceTemplate>();
    public DbSet<ProvisionedEnvironment> ProvisionedEnvironments => Set<ProvisionedEnvironment>();
    public DbSet<DeployedResource> DeployedResources => Set<DeployedResource>();
    public DbSet<DriftItem> DriftItems => Set<DriftItem>();
    public DbSet<EnvironmentActivity> EnvironmentActivities => Set<EnvironmentActivity>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<AgentDefinition> AgentDefinitions => Set<AgentDefinition>();
    public DbSet<ToolDefinition> ToolDefinitions => Set<ToolDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ──
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);

            entity.HasIndex(e => e.CacSubjectDN)
                .IsUnique()
                .HasDatabaseName("IX_User_CacSubjectDN");

            // Roles stored as JSON column
            entity.Property(e => e.Roles)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<UserRole[]>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? Array.Empty<UserRole>()
                );

            entity.Property(e => e.PimActiveTier)
                .HasConversion<string>();
        });

        // ── Configuration ── (1:1 with User)
        modelBuilder.Entity<Entities.Configuration>(entity =>
        {
            entity.HasKey(e => e.ConfigurationId);

            entity.HasIndex(e => e.UserId)
                .IsUnique()
                .HasDatabaseName("IX_Configuration_UserId");

            entity.HasOne(e => e.User)
                .WithOne(u => u.Configuration)
                .HasForeignKey<Entities.Configuration>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.CloudEnvironment).HasConversion<string>();
        });

        // ── RemediationBoard ──
        modelBuilder.Entity<RemediationBoard>(entity =>
        {
            entity.HasKey(e => e.BoardId);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── RemediationTask ──
        modelBuilder.Entity<RemediationTask>(entity =>
        {
            entity.HasKey(e => e.TaskId);

            entity.HasIndex(e => new { e.BoardId, e.Status })
                .HasDatabaseName("IX_Task_BoardId_Status");

            entity.HasIndex(e => e.AssigneeUserId)
                .HasDatabaseName("IX_Task_AssigneeUserId");

            entity.HasIndex(e => e.DisplayId)
                .IsUnique()
                .HasDatabaseName("IX_Task_DisplayId");

            entity.HasOne(e => e.Board)
                .WithMany(b => b.Tasks)
                .HasForeignKey(e => e.BoardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Assignee)
                .WithMany()
                .HasForeignKey(e => e.AssigneeUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Property(e => e.Severity).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();

            // IsOverdue is computed — ignore for EF
            entity.Ignore(e => e.IsOverdue);
        });

        // ── TaskComment ──
        modelBuilder.Entity<TaskComment>(entity =>
        {
            entity.HasKey(e => e.CommentId);

            entity.HasOne(e => e.Task)
                .WithMany(t => t.Comments)
                .HasForeignKey(e => e.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // ── IaCTemplate ──
        modelBuilder.Entity<IaCTemplate>(entity =>
        {
            entity.HasKey(e => e.TemplateId);

            entity.HasOne(e => e.User)
                .WithMany(u => u.IaCTemplates)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.GenerationMethod).HasConversion<string>();

            // IsExpired is computed — ignore for EF
            entity.Ignore(e => e.IsExpired);
        });

        // ── ServiceTemplate ──
        modelBuilder.Entity<ServiceTemplate>(entity =>
        {
            entity.HasKey(e => e.TemplateId);

            entity.HasIndex(e => new { e.Name, e.Version })
                .IsUnique()
                .HasDatabaseName("IX_ServiceTemplate_Name_Version");

            entity.HasIndex(e => new { e.Category, e.Status })
                .HasDatabaseName("IX_ServiceTemplate_Category_Status");

            entity.HasIndex(e => e.IsDeleted)
                .HasDatabaseName("IX_ServiceTemplate_IsDeleted");

            entity.Property(e => e.GitSyncStatus).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.Format).HasConversion<string>();

            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // ── ProvisionedEnvironment ──
        modelBuilder.Entity<ProvisionedEnvironment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.SubscriptionId, e.ResourceGroup })
                .HasDatabaseName("IX_ProvisionedEnvironment_Sub_RG");

            entity.HasIndex(e => new { e.Status, e.HasDrift })
                .HasDatabaseName("IX_ProvisionedEnvironment_Status_Drift");

            entity.HasIndex(e => e.TemplateId)
                .HasDatabaseName("IX_ProvisionedEnvironment_TemplateId");

            entity.HasIndex(e => e.IsDeleted)
                .HasDatabaseName("IX_ProvisionedEnvironment_IsDeleted");

            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.HasOne(e => e.Template)
                .WithMany()
                .HasForeignKey(e => e.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // ── DeployedResource ──
        modelBuilder.Entity<DeployedResource>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Environment)
                .WithMany(env => env.DeployedResources)
                .HasForeignKey(e => e.EnvironmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── DriftItem ──
        modelBuilder.Entity<DriftItem>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Severity).HasConversion<string>();

            entity.HasOne(e => e.Environment)
                .WithMany(env => env.DriftItems)
                .HasForeignKey(e => e.EnvironmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── EnvironmentActivity ──
        modelBuilder.Entity<EnvironmentActivity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.EnvironmentId, e.Timestamp })
                .IsDescending(false, true)
                .HasDatabaseName("IX_EnvironmentActivity_EnvId_Timestamp");

            entity.HasOne(e => e.Environment)
                .WithMany(env => env.Activities)
                .HasForeignKey(e => e.EnvironmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Alert ──
        modelBuilder.Entity<Alert>(entity =>
        {
            entity.HasKey(e => e.AlertId);

            entity.HasIndex(e => new { e.Severity, e.LifecycleState })
                .HasDatabaseName("IX_Alert_Severity_State");

            entity.HasIndex(e => new { e.GroupingKey, e.CreatedAt })
                .HasDatabaseName("IX_Alert_GroupingKey_CreatedAt");

            entity.Property(e => e.Severity).HasConversion<string>();
            entity.Property(e => e.LifecycleState).HasConversion<string>();
            entity.Property(e => e.Category).HasConversion<string>();
        });

        // ── AuditLogEntry ── (IMMUTABLE — append-only)
        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.HasKey(e => e.AuditLogId);

            entity.HasIndex(e => new { e.UserId, e.Timestamp })
                .IsDescending(false, true)
                .HasDatabaseName("IX_Audit_UserId_Timestamp");

            entity.HasIndex(e => e.CorrelationId)
                .HasDatabaseName("IX_Audit_CorrelationId");

            entity.HasIndex(e => e.Timestamp)
                .HasDatabaseName("IX_Audit_Timestamp");

            entity.Property(e => e.Outcome).HasConversion<string>();
        });

        // ── AgentDefinition ──
        modelBuilder.Entity<AgentDefinition>(entity =>
        {
            entity.HasKey(e => e.AgentId);

            entity.Property(e => e.HealthStatus).HasConversion<string>();
        });

        // ── ToolDefinition ──
        modelBuilder.Entity<ToolDefinition>(entity =>
        {
            entity.HasKey(e => e.ToolId);

            entity.HasOne(e => e.Agent)
                .WithMany(a => a.Tools)
                .HasForeignKey(e => e.AgentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.PimTierRequired).HasConversion<string>();
        });
    }
}
