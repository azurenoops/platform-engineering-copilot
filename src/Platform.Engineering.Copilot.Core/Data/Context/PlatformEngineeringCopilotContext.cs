using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Compliance.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Context;

/// <summary>
/// Platform Engineering Copilot Database Context.
/// 
/// ═══════════════════════════════════════════════════════════════════════════════
/// TWO TEMPLATE SYSTEMS - IMPORTANT ARCHITECTURAL DISTINCTION
/// ═══════════════════════════════════════════════════════════════════════════════
/// 
/// 1. IAC TEMPLATES (Infrastructure Agent) - AI-Generated, Temporary
///    ─────────────────────────────────────────────────────────────────────────
///    • Entity: InfrastructureTemplate, TemplateVersion, TemplateFile
///    • Service: TemplateStorageService → IInfrastructureTemplateRepository
///    • Purpose: AI generates custom Bicep/ARM/Terraform based on user requests
///    • Lifecycle: TEMPORARY - 30 minute expiry by default, auto-cleaned
///    • Use Case: "Create a storage account with private endpoints and geo-redundancy"
///    • Flow: User request → AI generates → Store temporarily → User deploys → Expires
/// 
/// 2. SERVICE TEMPLATES (Environment Agent) - Pre-Approved, Permanent
///    ─────────────────────────────────────────────────────────────────────────
///    • Entity: ServiceTemplateEntity, ServiceTemplateAuditEntity
///    • Service: ServiceTemplateCatalogService → IServiceTemplateRepository (future)
///    • Purpose: Platform team pre-approves infrastructure patterns for self-service
///    • Lifecycle: PERMANENT - versioned catalog with approval workflow
///    • Use Case: "Provision a production-ready AKS cluster from the approved template"
///    • Flow: Platform team creates → Approval workflow → Published → Developers provision
///    • Tracking: ProvisionedEnvironmentEntity tracks deployed instances with drift detection
/// 
/// ═══════════════════════════════════════════════════════════════════════════════
/// </summary>
public class PlatformEngineeringCopilotContext : DbContext
{
    public PlatformEngineeringCopilotContext(DbContextOptions<PlatformEngineeringCopilotContext> options)
        : base(options)
    {
    }

    #region ══════════════════════════════════════════════════════════════════════
    //  IAC TEMPLATES (Infrastructure Agent)
    //  AI-generated Bicep/ARM/Terraform with 30-minute expiry
    //  Service: TemplateStorageService → IInfrastructureTemplateRepository
    #endregion
    
    /// <summary>AI-generated IaC templates (temporary, 30-min expiry)</summary>
    public DbSet<InfrastructureTemplate> InfrastructureTemplates { get; set; }
    
    /// <summary>Version history for IaC templates</summary>
    public DbSet<TemplateVersion> TemplateVersions { get; set; }
    
    /// <summary>Individual files within multi-file IaC templates</summary>
    public DbSet<TemplateFile> TemplateFiles { get; set; }

    #region ══════════════════════════════════════════════════════════════════════
    //  IAC DEPLOYMENT TRACKING (Infrastructure Agent)
    //  Tracks deployments of AI-generated templates
    #endregion
    
    /// <summary>Deployment records for IaC template deployments</summary>
    public DbSet<InfrastructureDeployment> InfrastructureDeployments { get; set; }
    
    /// <summary>Deployment action history (start, stop, scale, etc.)</summary>
    public DbSet<DeploymentHistory> DeploymentHistory { get; set; }

    #region ══════════════════════════════════════════════════════════════════════
    //  SERVICE TEMPLATES (Environment Agent) 
    //  Pre-approved infrastructure patterns with approval workflow
    //  Service: ServiceTemplateCatalogService
    #endregion
    
    /// <summary>Pre-approved service templates (permanent, versioned catalog)</summary>
    public DbSet<ServiceTemplateEntity> ServiceTemplates { get; set; }
    
    /// <summary>Audit log for service template changes</summary>
    public DbSet<ServiceTemplateAuditEntity> ServiceTemplateAuditLogs { get; set; }
    
    /// <summary>Environments provisioned from service templates (with drift detection)</summary>
    public DbSet<ProvisionedEnvironmentEntity> ProvisionedEnvironments { get; set; }
    
    /// <summary>Individual Azure resources within provisioned environments</summary>
    public DbSet<DeployedResourceEntity> DeployedResources { get; set; }
    
    /// <summary>Configuration drift items detected in provisioned environments</summary>
    public DbSet<DriftItemEntity> DriftItems { get; set; }
    
    /// <summary>Audit log for provisioned environment changes</summary>
    public DbSet<EnvironmentAuditEntity> EnvironmentAuditLogs { get; set; }

    #region ══════════════════════════════════════════════════════════════════════
    //  SHARED INFRASTRUCTURE
    #endregion

    // Agent Configuration
    public DbSet<AgentConfiguration> AgentConfigurations { get; set; }

    // Semantic Processing (Chat intent recognition)
    public DbSet<SemanticIntent> SemanticIntents { get; set; }
    public DbSet<IntentFeedback> IntentFeedback { get; set; }
    public DbSet<IntentPattern> IntentPatterns { get; set; }

    // Governance and Approval Workflows
    public DbSet<ApprovalWorkflowEntity> ApprovalWorkflows { get; set; }

    // Compliance (NIST 800-53)
    public DbSet<ComplianceAssessment> ComplianceAssessments { get; set; }
    public DbSet<ComplianceFinding> ComplianceFindings { get; set; }

    // Audit Logging (NIST 800-53 AU-2, AU-3, AU-9)
    public DbSet<AuditLogEntity> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure entity relationships and constraints
        ConfigureInfrastructureTemplates(modelBuilder);
        ConfigureInfrastructureDeployments(modelBuilder);
        ConfigureSemanticIntents(modelBuilder);
        ConfigureApprovalWorkflows(modelBuilder);
        ConfigureComplianceAssessments(modelBuilder);
        ConfigureAuditLogs(modelBuilder);
        ConfigureAgentConfigurations(modelBuilder);
        ConfigureServiceTemplates(modelBuilder);
        ConfigureProvisionedEnvironments(modelBuilder);

        // Configure indexes for performance
        ConfigureIndexes(modelBuilder);
    }

    private static void ConfigureInfrastructureTemplates(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InfrastructureTemplate>(entity =>
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

    private static void ConfigureInfrastructureDeployments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InfrastructureDeployment>(entity =>
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

    private static void ConfigureApprovalWorkflows(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApprovalWorkflowEntity>(entity =>
        {
            // Indexes for common queries
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_ApprovalWorkflows_Status");

            entity.HasIndex(e => new { e.Status, e.Priority, e.CreatedAt })
                .HasDatabaseName("IX_ApprovalWorkflows_Status_Priority_CreatedAt");

            entity.HasIndex(e => e.RequestedBy)
                .HasDatabaseName("IX_ApprovalWorkflows_RequestedBy");

            entity.HasIndex(e => e.ResourceType)
                .HasDatabaseName("IX_ApprovalWorkflows_ResourceType");

            entity.HasIndex(e => e.Environment)
                .HasDatabaseName("IX_ApprovalWorkflows_Environment");

            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("IX_ApprovalWorkflows_ExpiresAt");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_ApprovalWorkflows_CreatedAt");

            // Properties
            entity.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.Priority)
                .HasDefaultValue(1);

            entity.Property(e => e.ExpiresAt)
                .IsRequired();
        });
    }

    
    private static void ConfigureComplianceAssessments(ModelBuilder modelBuilder)
    {
        // ComplianceAssessment configuration
        modelBuilder.Entity<ComplianceAssessment>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Indexes for performance
            entity.HasIndex(e => e.SubscriptionId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => new { e.SubscriptionId, e.AssessmentType });
            entity.HasIndex(e => new { e.Status, e.StartedAt });
            
            // Relationships
            entity.HasMany(e => e.Findings)
                  .WithOne(f => f.Assessment)
                  .HasForeignKey(f => f.AssessmentId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ComplianceFinding configuration
        modelBuilder.Entity<ComplianceFinding>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Indexes for performance
            entity.HasIndex(e => e.AssessmentId);
            entity.HasIndex(e => e.Severity);
            entity.HasIndex(e => e.ComplianceStatus);
            entity.HasIndex(e => e.FindingType);
            entity.HasIndex(e => e.RuleId);
            entity.HasIndex(e => e.ResourceType);
            entity.HasIndex(e => e.DetectedAt);
            entity.HasIndex(e => new { e.AssessmentId, e.Severity });
            entity.HasIndex(e => new { e.ComplianceStatus, e.Severity });
        });
    }


    /* private static void ConfigureServiceCreationRequests(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServiceCreationRequest>(entity =>
        {
            // Indexes for common queries
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_ServiceCreationRequests_Status");

            entity.HasIndex(e => e.MissionOwnerEmail)
                .HasDatabaseName("IX_ServiceCreationRequests_MissionOwnerEmail");

            entity.HasIndex(e => e.Command)
                .HasDatabaseName("IX_ServiceCreationRequests_Command");

            entity.HasIndex(e => e.ClassificationLevel)
                .HasDatabaseName("IX_ServiceCreationRequests_ClassificationLevel");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_ServiceCreationRequests_CreatedAt");

            entity.HasIndex(e => new { e.Status, e.Priority, e.CreatedAt })
                .HasDatabaseName("IX_ServiceCreationRequests_Status_Priority_CreatedAt");

            // Properties
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.LastUpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

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

    private static void ConfigureAuditLogs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLogEntity>(entity =>
        {
            // Table name already set via [Table("AuditLogs")] attribute
            
            // Configure timestamp with default value
            entity.Property(e => e.Timestamp)
                .HasDefaultValueSql("GETUTCDATE()")
                .IsRequired();

            // Configure EntryId as primary key (already set via [Key] attribute)
            entity.Property(e => e.EntryId)
                .HasMaxLength(50)
                .IsRequired();

            // Severity stored as int (enum)
            entity.Property(e => e.Severity)
                .IsRequired();

            // Required string fields
            entity.Property(e => e.EventType)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.ActorId)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Action)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Result)
                .HasMaxLength(50)
                .IsRequired();

            // JSON columns for complex data (already configured via [Column(TypeName = "nvarchar(max)")] attributes)
            // These will be serialized/deserialized in the service layer

            // Configure for optimistic concurrency
            entity.Property(e => e.RowVersion)
                .IsRowVersion();

            // Default values for flags
            entity.Property(e => e.IsArchived)
                .HasDefaultValue(false);

            // Index configurations are in ConfigureIndexes() method
        });
    }

    private static void ConfigureAgentConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AgentConfiguration>(entity =>
        {
            // Unique constraint on AgentName
            entity.HasIndex(e => e.AgentName).IsUnique();
            
            // Indexes for common queries
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => new { e.Category, e.IsEnabled, e.DisplayOrder });
            
            // Default values
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.Property(e => e.HealthStatus).HasDefaultValue("Unknown");
        });
    }

    /// <summary>
    /// Configure Platform Engineering Service Templates
    /// (Pre-approved infrastructure patterns for self-service provisioning by developers)
    /// </summary>
    private static void ConfigureServiceTemplates(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServiceTemplateEntity>(entity =>
        {
            // Unique constraint on Name + Version
            entity.HasIndex(e => new { e.Name, e.Version }).IsUnique();

            // Indexes for common queries
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.CreatedBy);
            entity.HasIndex(e => new { e.Category, e.Status });

            // Default values
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.RequiresApproval).HasDefaultValue(true);
            entity.Property(e => e.EnforceCompliance).HasDefaultValue(true);
            entity.Property(e => e.DeploymentCount).HasDefaultValue(0);
            entity.Property(e => e.GitAutoSync).HasDefaultValue(true);
            entity.Property(e => e.GitSyncIntervalMinutes).HasDefaultValue(15);
        });

        modelBuilder.Entity<ServiceTemplateAuditEntity>(entity =>
        {
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.HasIndex(e => e.PerformedBy);
            entity.HasIndex(e => e.Action);

            entity.Property(e => e.Timestamp).HasDefaultValueSql("GETUTCDATE()");
        });
    }

    /// <summary>
    /// Configure Provisioned Environments (environments created from Service Templates)
    /// </summary>
    private static void ConfigureProvisionedEnvironments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProvisionedEnvironmentEntity>(entity =>
        {
            // Indexes for common queries
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.TemplateId);
            entity.HasIndex(e => e.SubscriptionId);
            entity.HasIndex(e => e.ResourceGroup);
            entity.HasIndex(e => e.CreatedBy);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.OwnerEmail);
            entity.HasIndex(e => e.HasDrift);
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => new { e.SubscriptionId, e.ResourceGroup });
            entity.HasIndex(e => new { e.Status, e.HasDrift });

            // Default values
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.Status).HasDefaultValue("Provisioning");
            entity.Property(e => e.HasDrift).HasDefaultValue(false);
            entity.Property(e => e.DriftCount).HasDefaultValue(0);
            entity.Property(e => e.AutoDelete).HasDefaultValue(false);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            // Soft delete filter
            entity.HasQueryFilter(e => !e.IsDeleted);

            // Relationships
            entity.HasOne(e => e.Template)
                .WithMany(t => t.ProvisionedEnvironments)
                .HasForeignKey(e => e.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ClonedFrom)
                .WithMany(e => e.ClonedEnvironments)
                .HasForeignKey(e => e.ClonedFromId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DeployedResourceEntity>(entity =>
        {
            entity.HasIndex(e => e.ProvisionedEnvironmentId);
            entity.HasIndex(e => e.ResourceType);
            entity.HasIndex(e => e.ProvisioningState);

            entity.Property(e => e.DeployedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.ProvisionedEnvironment)
                .WithMany()
                .HasForeignKey(e => e.ProvisionedEnvironmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DriftItemEntity>(entity =>
        {
            entity.HasIndex(e => e.ProvisionedEnvironmentId);
            entity.HasIndex(e => e.Severity);
            entity.HasIndex(e => e.IsRemediated);
            entity.HasIndex(e => new { e.ProvisionedEnvironmentId, e.IsRemediated });

            entity.Property(e => e.DetectedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.DriftType).HasDefaultValue("Configuration");
            entity.Property(e => e.Severity).HasDefaultValue("Warning");
            entity.Property(e => e.CanAutoRemediate).HasDefaultValue(false);
            entity.Property(e => e.IsRemediated).HasDefaultValue(false);

            entity.HasOne(e => e.ProvisionedEnvironment)
                .WithMany()
                .HasForeignKey(e => e.ProvisionedEnvironmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureIndexes(ModelBuilder modelBuilder)
    {
        // Additional composite indexes for common query patterns
        modelBuilder.Entity<InfrastructureDeployment>()
            .HasIndex(e => new { e.SubscriptionId, e.ResourceGroupName, e.Status })
            .HasDatabaseName("IX_InfrastructureDeployments_Subscription_ResourceGroup_Status");

        // Approval Workflows indexes
        modelBuilder.Entity<ApprovalWorkflowEntity>()
            .HasIndex(e => new { e.ResourceGroupName, e.Environment, e.Status })
            .HasDatabaseName("IX_ApprovalWorkflows_ResourceGroup_Environment_Status");

        // Audit Logs indexes (for performance and compliance queries)
        modelBuilder.Entity<AuditLogEntity>()
            .HasIndex(e => new { e.Timestamp, e.Severity })
            .HasDatabaseName("IX_AuditLogs_Time_Severity");

        modelBuilder.Entity<AuditLogEntity>()
            .HasIndex(e => new { e.ActorId, e.Timestamp })
            .HasDatabaseName("IX_AuditLogs_Actor_Time");

        modelBuilder.Entity<AuditLogEntity>()
            .HasIndex(e => new { e.ResourceId, e.Action, e.Timestamp })
            .HasDatabaseName("IX_AuditLogs_Resource_Action_Time");
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
            .Where(e => e.Entity is InfrastructureTemplate or InfrastructureDeployment or IntentPattern or AgentConfiguration or ServiceTemplateEntity or ProvisionedEnvironmentEntity &&
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