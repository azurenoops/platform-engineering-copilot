using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data;

/// <summary>
/// Main DbContext for all platform operational entities (16 entities).
/// Compliance, infrastructure, remediation, audit, configuration data.
/// </summary>
public class PlatformEngineeringCopilotContext : DbContext
{
    public PlatformEngineeringCopilotContext(DbContextOptions<PlatformEngineeringCopilotContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Configuration> Configurations => Set<Configuration>();
    public DbSet<ComplianceAssessment> ComplianceAssessments => Set<ComplianceAssessment>();
    public DbSet<ComplianceFinding> ComplianceFindings => Set<ComplianceFinding>();
    public DbSet<RemediationBoard> RemediationBoards => Set<RemediationBoard>();
    public DbSet<RemediationTask> RemediationTasks => Set<RemediationTask>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<EvidencePackage> EvidencePackages => Set<EvidencePackage>();
    public DbSet<ComplianceDocument> ComplianceDocuments => Set<ComplianceDocument>();
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
        modelBuilder.Entity<Configuration>(entity =>
        {
            entity.HasKey(e => e.ConfigurationId);

            entity.HasIndex(e => e.UserId)
                .IsUnique()
                .HasDatabaseName("IX_Configuration_UserId");

            entity.HasOne(e => e.User)
                .WithOne(u => u.Configuration)
                .HasForeignKey<Configuration>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.CloudEnvironment).HasConversion<string>();
            entity.Property(e => e.DefaultFramework).HasConversion<string>();
            entity.Property(e => e.Baseline).HasConversion<string>();
            entity.Property(e => e.DefaultScanType).HasConversion<string>();
        });

        // ── ComplianceAssessment ──
        modelBuilder.Entity<ComplianceAssessment>(entity =>
        {
            entity.HasKey(e => e.AssessmentId);

            entity.HasIndex(e => new { e.UserId, e.CreatedAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_Assessment_UserId_CreatedAt");

            entity.HasIndex(e => e.SubscriptionId)
                .HasDatabaseName("IX_Assessment_SubscriptionId");

            entity.HasOne(e => e.User)
                .WithMany(u => u.Assessments)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.ScanType).HasConversion<string>();
            entity.Property(e => e.Framework).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // ── ComplianceFinding ──
        modelBuilder.Entity<ComplianceFinding>(entity =>
        {
            entity.HasKey(e => e.FindingId);

            entity.HasIndex(e => e.AssessmentId)
                .HasDatabaseName("IX_Finding_AssessmentId");

            entity.HasIndex(e => new { e.ControlFamily, e.Severity })
                .HasDatabaseName("IX_Finding_ControlFamily_Severity");

            entity.HasOne(e => e.Assessment)
                .WithMany(a => a.Findings)
                .HasForeignKey(e => e.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Severity).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
        });

        // ── RemediationBoard ──
        modelBuilder.Entity<RemediationBoard>(entity =>
        {
            entity.HasKey(e => e.BoardId);

            entity.HasOne(e => e.Assessment)
                .WithOne(a => a.Board)
                .HasForeignKey<RemediationBoard>(e => e.AssessmentId)
                .OnDelete(DeleteBehavior.Restrict);

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

            entity.HasOne(e => e.Finding)
                .WithOne(f => f.RemediationTask)
                .HasForeignKey<RemediationTask>(e => e.FindingId)
                .OnDelete(DeleteBehavior.Restrict);

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

        // ── EvidencePackage ──
        modelBuilder.Entity<EvidencePackage>(entity =>
        {
            entity.HasKey(e => e.PackageId);

            entity.HasIndex(e => e.ControlId)
                .HasDatabaseName("IX_Evidence_ControlId");

            entity.HasOne(e => e.Assessment)
                .WithMany(a => a.EvidencePackages)
                .HasForeignKey(e => e.AssessmentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.User)
                .WithMany(u => u.EvidencePackages)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // ── ComplianceDocument ──
        modelBuilder.Entity<ComplianceDocument>(entity =>
        {
            entity.HasKey(e => e.DocumentId);

            entity.HasOne(e => e.Assessment)
                .WithMany(a => a.Documents)
                .HasForeignKey(e => e.AssessmentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Documents)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.DocumentType).HasConversion<string>();
            entity.Property(e => e.Framework).HasConversion<string>();

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
            entity.Property(e => e.Framework).HasConversion<string>();

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
