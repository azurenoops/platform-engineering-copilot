using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Data.Entities;

namespace Platform.Engineering.Copilot.Data.Context;

/// <summary>
/// Environment Management Database Context
/// </summary>
public class EnvironmentManagementContext : DbContext
{
    public EnvironmentManagementContext(DbContextOptions<EnvironmentManagementContext> options)
        : base(options)
    {
    }

    // Environment Templates
    public DbSet<EnvironmentTemplate> EnvironmentTemplates { get; set; }
    public DbSet<TemplateVersion> TemplateVersions { get; set; }
    public DbSet<TemplateFile> TemplateFiles { get; set; }

    // Environment Deployments
    public DbSet<EnvironmentDeployment> EnvironmentDeployments { get; set; }
    public DbSet<DeploymentHistory> DeploymentHistory { get; set; }

    // Scaling
    public DbSet<ScalingPolicy> ScalingPolicies { get; set; }
    public DbSet<ScalingEvent> ScalingEvents { get; set; }

    // Metrics & Compliance
    public DbSet<EnvironmentMetrics> EnvironmentMetrics { get; set; }
    public DbSet<ComplianceScan> ComplianceScans { get; set; }
    public DbSet<ComplianceFinding> ComplianceFindings { get; set; }

    // Semantic Processing
    public DbSet<SemanticIntent> SemanticIntents { get; set; }
    public DbSet<IntentFeedback> IntentFeedback { get; set; }
    public DbSet<IntentPattern> IntentPatterns { get; set; }

    // Enhanced Environment Management
    public DbSet<EnvironmentLifecycle> EnvironmentLifecycles { get; set; }
    public DbSet<EnvironmentActivity> EnvironmentActivities { get; set; }
    public DbSet<EnvironmentCostTracking> EnvironmentCostTrackings { get; set; }
    public DbSet<EnvironmentClone> EnvironmentClones { get; set; }
    public DbSet<EnvironmentSynchronization> EnvironmentSynchronizations { get; set; }

    // Navy Flankspeed Onboarding
    public DbSet<OnboardingRequest> OnboardingRequests { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure entity relationships and constraints
        ConfigureEnvironmentTemplates(modelBuilder);
        ConfigureEnvironmentDeployments(modelBuilder);
        ConfigureScalingPolicies(modelBuilder);
        ConfigureMetricsAndCompliance(modelBuilder);
        ConfigureSemanticIntents(modelBuilder);
        ConfigureEnvironmentLifecycle(modelBuilder);
        //ConfigureOnboardingRequests(modelBuilder);

        // Configure indexes for performance
        ConfigureIndexes(modelBuilder);
    }

    private static void ConfigureEnvironmentTemplates(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EnvironmentTemplate>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => new { e.TemplateType, e.DeploymentTier });
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.IsActive);

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<TemplateVersion>(entity =>
        {
            entity.HasIndex(e => new { e.TemplateId, e.Version }).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
        });
    }

    private static void ConfigureEnvironmentDeployments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EnvironmentDeployment>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => new { e.EnvironmentType, e.Status });
            entity.HasIndex(e => e.ResourceGroupName);
            entity.HasIndex(e => e.SubscriptionId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.IsDeleted);

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Soft delete filter
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<DeploymentHistory>(entity =>
        {
            entity.HasIndex(e => new { e.DeploymentId, e.StartedAt });
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.Status);
        });
    }

    private static void ConfigureScalingPolicies(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScalingPolicy>(entity =>
        {
            entity.HasIndex(e => new { e.DeploymentId, e.PolicyType });
            entity.HasIndex(e => e.IsActive);

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<ScalingEvent>(entity =>
        {
            entity.HasIndex(e => new { e.PolicyId, e.CreatedAt });
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.Status);
        });
    }

    private static void ConfigureMetricsAndCompliance(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EnvironmentMetrics>(entity =>
        {
            entity.HasIndex(e => new { e.DeploymentId, e.MetricType, e.Timestamp });
            entity.HasIndex(e => new { e.MetricName, e.Timestamp });
        });

        modelBuilder.Entity<ComplianceScan>(entity =>
        {
            entity.HasIndex(e => new { e.DeploymentId, e.StartedAt });
            entity.HasIndex(e => new { e.Standard, e.Status });
            entity.HasIndex(e => e.ComplianceScore);
        });

        modelBuilder.Entity<ComplianceFinding>(entity =>
        {
            entity.HasIndex(e => new { e.ScanId, e.Severity });
            entity.HasIndex(e => e.RuleId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.IsRemediable);
        });
    }

    private static void ConfigureSemanticIntents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SemanticIntent>(entity =>
        {
            entity.HasIndex(e => new { e.IntentCategory, e.IntentAction });
            entity.HasIndex(e => new { e.UserId, e.CreatedAt });
            entity.HasIndex(e => e.Confidence);
            entity.HasIndex(e => e.WasSuccessful);

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<IntentFeedback>(entity =>
        {
            entity.HasIndex(e => new { e.IntentId, e.FeedbackType });
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<IntentPattern>(entity =>
        {
            entity.HasIndex(e => new { e.IntentCategory, e.IntentAction });
            entity.HasIndex(e => e.SuccessRate);
            entity.HasIndex(e => e.IsActive);

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });
    }

    private static void ConfigureEnvironmentLifecycle(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EnvironmentLifecycle>(entity =>
        {
            entity.HasIndex(e => new { e.LifecycleType, e.Status });
            entity.HasIndex(e => e.OwnerTeam);
            entity.HasIndex(e => e.Project);
            entity.HasIndex(e => e.ScheduledEndTime);
            entity.HasIndex(e => e.LastActivityAt);

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<EnvironmentActivity>(entity =>
        {
            entity.HasIndex(e => new { e.EnvironmentLifecycleId, e.Timestamp });
            entity.HasIndex(e => e.ActivityType);
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<EnvironmentCostTracking>(entity =>
        {
            entity.HasIndex(e => new { e.EnvironmentLifecycleId, e.Date });
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => e.DailyCost);
        });

        modelBuilder.Entity<EnvironmentClone>(entity =>
        {
            entity.HasIndex(e => new { e.SourceEnvironmentId, e.TargetEnvironmentId });
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartedAt);
        });

        modelBuilder.Entity<EnvironmentSynchronization>(entity =>
        {
            entity.HasIndex(e => new { e.SourceEnvironmentId, e.TargetEnvironmentId });
            entity.HasIndex(e => e.SyncType);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.NextSyncAt);
        });
    }

    /* private static void ConfigureOnboardingRequests(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OnboardingRequest>(entity =>
        {
            // Indexes for common queries
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_OnboardingRequests_Status");

            entity.HasIndex(e => e.MissionOwnerEmail)
                .HasDatabaseName("IX_OnboardingRequests_MissionOwnerEmail");

            entity.HasIndex(e => e.Command)
                .HasDatabaseName("IX_OnboardingRequests_Command");

            entity.HasIndex(e => e.ClassificationLevel)
                .HasDatabaseName("IX_OnboardingRequests_ClassificationLevel");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_OnboardingRequests_CreatedAt");

            entity.HasIndex(e => new { e.Status, e.Priority, e.CreatedAt })
                .HasDatabaseName("IX_OnboardingRequests_Status_Priority_CreatedAt");

            // Properties
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now')");

            entity.Property(e => e.LastUpdatedAt)
                .HasDefaultValueSql("datetime('now')");

            entity.Property(e => e.Priority)
                .HasDefaultValue(3);

            entity.Property(e => e.RequiresCac)
                .HasDefaultValue(true);

            entity.Property(e => e.DataResidency)
                .HasDefaultValue("US")
                .HasMaxLength(50);

            entity.Property(e => e.ClassificationLevel)
                .HasDefaultValue("UNCLASS")
                .HasMaxLength(20);

            entity.Property(e => e.Region)
                .HasDefaultValue("usgovvirginia")
                .HasMaxLength(50);
        });
    } */

    private static void ConfigureIndexes(ModelBuilder modelBuilder)
    {
        // Additional composite indexes for common query patterns
        modelBuilder.Entity<EnvironmentDeployment>()
            .HasIndex(e => new { e.SubscriptionId, e.ResourceGroupName, e.Status })
            .HasDatabaseName("IX_EnvironmentDeployments_Subscription_ResourceGroup_Status");

        modelBuilder.Entity<EnvironmentMetrics>()
            .HasIndex(e => new { e.DeploymentId, e.MetricType, e.Timestamp })
            .HasDatabaseName("IX_EnvironmentMetrics_Deployment_Type_Time");

        modelBuilder.Entity<ComplianceScan>()
            .HasIndex(e => new { e.DeploymentId, e.Standard, e.CompletedAt })
            .HasDatabaseName("IX_ComplianceScans_Deployment_Standard_Completed");

        // Enhanced Environment Management indexes
        modelBuilder.Entity<EnvironmentLifecycle>()
            .HasIndex(e => new { e.OwnerTeam, e.Project, e.Status })
            .HasDatabaseName("IX_EnvironmentLifecycles_Team_Project_Status");

        modelBuilder.Entity<EnvironmentCostTracking>()
            .HasIndex(e => new { e.Date, e.DailyCost })
            .HasDatabaseName("IX_EnvironmentCostTrackings_Date_Cost");
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is EnvironmentTemplate or EnvironmentDeployment or ScalingPolicy or IntentPattern &&
                       (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entityEntry in entries)
        {
            if (entityEntry.State == EntityState.Added)
            {
                if (entityEntry.Property("CreatedAt") != null)
                    entityEntry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
            }

            if (entityEntry.Property("UpdatedAt") != null)
                entityEntry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
        }
    }
}