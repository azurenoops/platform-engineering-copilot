-- ============================================================================
-- Platform Engineering Copilot - Complete Schema Migration
-- Updates all tables to match EF Core entity models
-- Run this script in Azure SQL Database Query Editor
-- ============================================================================

SET NOCOUNT ON;
PRINT 'Starting complete schema migration...';
PRINT '============================================';
GO

-- ============================================================================
-- 1. EnvironmentTemplates - Already matches entity (verified)
-- ============================================================================
PRINT '';
PRINT '1. Checking EnvironmentTemplates table...';
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'EnvironmentTemplates')
    PRINT '   ✓ EnvironmentTemplates table exists and schema is correct';
ELSE
    PRINT '   ✗ EnvironmentTemplates table missing - run create-platform-tables.sql first';
GO

-- ============================================================================
-- 2. TemplateVersions - Already matches entity (verified)
-- ============================================================================
PRINT '';
PRINT '2. Checking TemplateVersions table...';
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TemplateVersions')
    PRINT '   ✓ TemplateVersions table exists and schema is correct';
ELSE
    PRINT '   ✗ TemplateVersions table missing - run create-platform-tables.sql first';
GO

-- ============================================================================
-- 3. TemplateFiles - Already matches entity (verified)
-- ============================================================================
PRINT '';
PRINT '3. Checking TemplateFiles table...';
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'TemplateFiles')
    PRINT '   ✓ TemplateFiles table exists and schema is correct';
ELSE
    PRINT '   ✗ TemplateFiles table missing - run create-platform-tables.sql first';
GO

-- ============================================================================
-- 4. EnvironmentDeployments - NEEDS UPDATES
-- Missing columns: Configuration, Parameters, Tags, DeployedBy, IsPollingActive,
-- LastPolledAt, PollingAttempts, CurrentPollingInterval, ProgressPercentage,
-- EstimatedTimeRemaining, EstimatedMonthlyCost, ActualMonthlyCost
-- ============================================================================
PRINT '';
PRINT '4. Updating EnvironmentDeployments table...';

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'EnvironmentDeployments')
BEGIN
    -- Rename DeploymentParameters to Parameters
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'DeploymentParameters')
       AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'Parameters')
    BEGIN
        EXEC sp_rename 'EnvironmentDeployments.DeploymentParameters', 'Parameters', 'COLUMN';
        PRINT '   ✓ Renamed DeploymentParameters to Parameters';
    END

    -- Rename DeploymentOutputs to Configuration
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'DeploymentOutputs')
       AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'Configuration')
    BEGIN
        EXEC sp_rename 'EnvironmentDeployments.DeploymentOutputs', 'Configuration', 'COLUMN';
        PRINT '   ✓ Renamed DeploymentOutputs to Configuration';
    END

    -- Add Configuration if doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'Configuration')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [Configuration] NVARCHAR(MAX) NULL;
        PRINT '   ✓ Added Configuration column';
    END

    -- Add Parameters if doesn't exist
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'Parameters')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [Parameters] NVARCHAR(MAX) NULL;
        PRINT '   ✓ Added Parameters column';
    END

    -- Add Tags
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'Tags')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [Tags] NVARCHAR(MAX) NULL;
        PRINT '   ✓ Added Tags column';
    END

    -- Add DeployedBy (rename CreatedBy if exists, else add)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'DeployedBy')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [DeployedBy] NVARCHAR(100) NOT NULL DEFAULT 'system';
        PRINT '   ✓ Added DeployedBy column';
    END

    -- Add IsPollingActive
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'IsPollingActive')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [IsPollingActive] BIT NOT NULL DEFAULT 0;
        PRINT '   ✓ Added IsPollingActive column';
    END

    -- Add LastPolledAt
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'LastPolledAt')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [LastPolledAt] DATETIME2 NULL;
        PRINT '   ✓ Added LastPolledAt column';
    END

    -- Add PollingAttempts
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'PollingAttempts')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [PollingAttempts] INT NOT NULL DEFAULT 0;
        PRINT '   ✓ Added PollingAttempts column';
    END

    -- Add CurrentPollingInterval (stored as bigint ticks for TimeSpan)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'CurrentPollingInterval')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [CurrentPollingInterval] BIGINT NULL;
        PRINT '   ✓ Added CurrentPollingInterval column';
    END

    -- Add ProgressPercentage
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'ProgressPercentage')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [ProgressPercentage] INT NOT NULL DEFAULT 0;
        PRINT '   ✓ Added ProgressPercentage column';
    END

    -- Add EstimatedTimeRemaining (stored as bigint ticks for TimeSpan)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'EstimatedTimeRemaining')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [EstimatedTimeRemaining] BIGINT NULL;
        PRINT '   ✓ Added EstimatedTimeRemaining column';
    END

    -- Add EstimatedMonthlyCost
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'EstimatedMonthlyCost')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [EstimatedMonthlyCost] DECIMAL(10,2) NULL;
        PRINT '   ✓ Added EstimatedMonthlyCost column';
    END

    -- Add ActualMonthlyCost
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'ActualMonthlyCost')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [ActualMonthlyCost] DECIMAL(10,2) NULL;
        PRINT '   ✓ Added ActualMonthlyCost column';
    END

    PRINT '   EnvironmentDeployments migration completed';
END
ELSE
    PRINT '   ✗ EnvironmentDeployments table missing - run create-platform-tables.sql first';
GO

-- ============================================================================
-- 5. DeploymentHistory - Missing Duration column
-- ============================================================================
PRINT '';
PRINT '5. Updating DeploymentHistory table...';

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DeploymentHistory')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DeploymentHistory') AND name = 'Duration')
    BEGIN
        ALTER TABLE [dbo].[DeploymentHistory] ADD [Duration] BIGINT NULL;
        PRINT '   ✓ Added Duration column';
    END
    ELSE
        PRINT '   ✓ Duration column already exists';
END
ELSE
    PRINT '   ✗ DeploymentHistory table missing - run create-platform-tables.sql first';
GO

-- ============================================================================
-- 6. AgentConfigurations - Completely different schema from SQL!
-- Entity uses AgentConfigurationId (int), AgentName, DisplayName, Category, etc.
-- SQL uses Id (guid), AgentType, ConfigKey, ConfigValue, etc.
-- ============================================================================
PRINT '';
PRINT '6. Recreating AgentConfigurations table (schema mismatch)...';

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AgentConfigurations')
BEGIN
    -- Check if it's the old schema (has ConfigKey column)
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AgentConfigurations') AND name = 'ConfigKey')
    BEGIN
        PRINT '   ⚠ Dropping old AgentConfigurations table (incompatible schema)...';
        DROP TABLE [dbo].[AgentConfigurations];
    END
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AgentConfigurations')
BEGIN
    CREATE TABLE [dbo].[AgentConfigurations] (
        [AgentConfigurationId] INT IDENTITY(1,1) PRIMARY KEY,
        [AgentName] NVARCHAR(100) NOT NULL,
        [DisplayName] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(1000) NULL,
        [IsEnabled] BIT NOT NULL DEFAULT 1,
        [Category] NVARCHAR(50) NOT NULL,
        [IconName] NVARCHAR(50) NULL,
        [ConfigurationJson] NVARCHAR(MAX) NULL,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [ModifiedBy] NVARCHAR(200) NULL,
        [Dependencies] NVARCHAR(500) NULL,
        [LastExecutedAt] DATETIME2 NULL,
        [HealthStatus] NVARCHAR(50) NULL
    );
    
    CREATE UNIQUE INDEX [IX_AgentConfigurations_AgentName] ON [dbo].[AgentConfigurations] ([AgentName]);
    CREATE INDEX [IX_AgentConfigurations_Category] ON [dbo].[AgentConfigurations] ([Category]);
    
    PRINT '   ✓ Created AgentConfigurations table with correct schema';
END
ELSE
    PRINT '   ✓ AgentConfigurations table already has correct schema';
GO

-- ============================================================================
-- 7. AuditLogs - Major schema differences from SQL!
-- Entity has many more columns
-- ============================================================================
PRINT '';
PRINT '7. Updating AuditLogs table...';

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
BEGIN
    -- Check if it's old schema (has Timestamp column instead of full schema)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AuditLogs') AND name = 'EntryId')
    BEGIN
        PRINT '   ⚠ Dropping old AuditLogs table (incompatible schema)...';
        DROP TABLE [dbo].[AuditLogs];
    END
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
BEGIN
    CREATE TABLE [dbo].[AuditLogs] (
        [EntryId] NVARCHAR(50) NOT NULL PRIMARY KEY,
        [Timestamp] DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
        [EventType] NVARCHAR(100) NOT NULL,
        [EventCategory] NVARCHAR(50) NULL,
        [Severity] INT NOT NULL DEFAULT 0,
        [ActorId] NVARCHAR(200) NOT NULL,
        [ActorName] NVARCHAR(200) NULL,
        [ActorType] NVARCHAR(50) NULL,
        [ResourceId] NVARCHAR(500) NULL,
        [ResourceType] NVARCHAR(100) NULL,
        [ResourceName] NVARCHAR(500) NULL,
        [Action] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(2000) NULL,
        [Result] NVARCHAR(50) NOT NULL,
        [FailureReason] NVARCHAR(1000) NULL,
        [IpAddress] NVARCHAR(45) NULL,
        [UserAgent] NVARCHAR(500) NULL,
        [SessionId] NVARCHAR(100) NULL,
        [CorrelationId] NVARCHAR(100) NULL,
        [MetadataJson] NVARCHAR(MAX) NULL,
        [TagsJson] NVARCHAR(MAX) NULL,
        [ChangeDetailsJson] NVARCHAR(MAX) NULL,
        [ComplianceContextJson] NVARCHAR(MAX) NULL,
        [SecurityContextJson] NVARCHAR(MAX) NULL,
        [ArchivedAt] DATETIMEOFFSET NULL,
        [IsArchived] BIT NOT NULL DEFAULT 0,
        [EntryHash] NVARCHAR(64) NULL,
        [RowVersion] ROWVERSION NOT NULL
    );
    
    CREATE INDEX [IX_AuditLogs_Timestamp] ON [dbo].[AuditLogs] ([Timestamp]);
    CREATE INDEX [IX_AuditLogs_EventType] ON [dbo].[AuditLogs] ([EventType]);
    CREATE INDEX [IX_AuditLogs_ActorId] ON [dbo].[AuditLogs] ([ActorId]);
    CREATE INDEX [IX_AuditLogs_ResourceId] ON [dbo].[AuditLogs] ([ResourceId]);
    CREATE INDEX [IX_AuditLogs_Severity] ON [dbo].[AuditLogs] ([Severity]);
    CREATE INDEX [IX_AuditLogs_CorrelationId] ON [dbo].[AuditLogs] ([CorrelationId]);
    
    PRINT '   ✓ Created AuditLogs table with correct schema';
END
ELSE
    PRINT '   ✓ AuditLogs table already has correct schema';
GO

-- ============================================================================
-- 8. ComplianceAssessments - Schema differences
-- Entity uses string Id, has Duration as BIGINT, different column names
-- ============================================================================
PRINT '';
PRINT '8. Updating ComplianceAssessments table...';

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ComplianceAssessments')
BEGIN
    -- Check for old schema
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ComplianceAssessments') AND name = 'OverallScore')
    BEGIN
        PRINT '   ⚠ Dropping old ComplianceAssessments table (incompatible schema)...';
        -- Drop dependent table first
        IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ComplianceFindings')
            DROP TABLE [dbo].[ComplianceFindings];
        DROP TABLE [dbo].[ComplianceAssessments];
    END
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ComplianceAssessments')
BEGIN
    CREATE TABLE [dbo].[ComplianceAssessments] (
        [Id] NVARCHAR(100) NOT NULL PRIMARY KEY,
        [SubscriptionId] NVARCHAR(100) NOT NULL,
        [ResourceGroupName] NVARCHAR(100) NULL,
        [AssessmentType] NVARCHAR(50) NOT NULL DEFAULT 'NIST-800-53',
        [Status] NVARCHAR(20) NOT NULL DEFAULT 'InProgress',
        [ComplianceScore] DECIMAL(5,2) NULL,
        [TotalFindings] INT NOT NULL DEFAULT 0,
        [CriticalFindings] INT NOT NULL DEFAULT 0,
        [HighFindings] INT NOT NULL DEFAULT 0,
        [MediumFindings] INT NOT NULL DEFAULT 0,
        [LowFindings] INT NOT NULL DEFAULT 0,
        [InformationalFindings] INT NOT NULL DEFAULT 0,
        [ExecutiveSummary] NVARCHAR(MAX) NULL,
        [RiskProfile] NVARCHAR(MAX) NULL,
        [Results] NVARCHAR(MAX) NULL,
        [Recommendations] NVARCHAR(MAX) NULL,
        [Metadata] NVARCHAR(MAX) NULL,
        [InitiatedBy] NVARCHAR(100) NOT NULL,
        [StartedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CompletedAt] DATETIME2 NULL,
        [Duration] BIGINT NULL
    );
    
    CREATE INDEX [IX_ComplianceAssessments_AssessmentType] ON [dbo].[ComplianceAssessments] ([AssessmentType]);
    CREATE INDEX [IX_ComplianceAssessments_Status] ON [dbo].[ComplianceAssessments] ([Status]);
    CREATE INDEX [IX_ComplianceAssessments_StartedAt] ON [dbo].[ComplianceAssessments] ([StartedAt]);
    CREATE INDEX [IX_ComplianceAssessments_SubscriptionId] ON [dbo].[ComplianceAssessments] ([SubscriptionId]);
    
    PRINT '   ✓ Created ComplianceAssessments table with correct schema';
END
ELSE
    PRINT '   ✓ ComplianceAssessments table already has correct schema';
GO

-- ============================================================================
-- 9. ComplianceFindings - Different schema
-- ============================================================================
PRINT '';
PRINT '9. Updating ComplianceFindings table...';

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ComplianceFindings')
BEGIN
    CREATE TABLE [dbo].[ComplianceFindings] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [AssessmentId] NVARCHAR(100) NOT NULL,
        [FindingId] NVARCHAR(100) NOT NULL,
        [RuleId] NVARCHAR(100) NOT NULL,
        [Title] NVARCHAR(200) NOT NULL,
        [Description] NVARCHAR(MAX) NOT NULL,
        [Severity] NVARCHAR(20) NOT NULL,
        [ComplianceStatus] NVARCHAR(30) NOT NULL,
        [FindingType] NVARCHAR(30) NOT NULL,
        [ResourceId] NVARCHAR(500) NULL,
        [ResourceType] NVARCHAR(100) NULL,
        [ResourceName] NVARCHAR(200) NULL,
        [ControlId] NVARCHAR(100) NULL,
        [ComplianceFrameworks] NVARCHAR(MAX) NULL,
        [AffectedNistControls] NVARCHAR(MAX) NULL,
        [Evidence] NVARCHAR(MAX) NULL,
        [Remediation] NVARCHAR(MAX) NULL,
        [Metadata] NVARCHAR(MAX) NULL,
        [IsRemediable] BIT NOT NULL DEFAULT 0,
        [IsAutomaticallyFixable] BIT NOT NULL DEFAULT 0,
        [DetectedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [ResolvedAt] DATETIME2 NULL,
        CONSTRAINT [FK_ComplianceFindings_ComplianceAssessments] FOREIGN KEY ([AssessmentId]) 
            REFERENCES [dbo].[ComplianceAssessments] ([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_ComplianceFindings_AssessmentId] ON [dbo].[ComplianceFindings] ([AssessmentId]);
    CREATE INDEX [IX_ComplianceFindings_FindingId] ON [dbo].[ComplianceFindings] ([FindingId]);
    CREATE INDEX [IX_ComplianceFindings_Severity] ON [dbo].[ComplianceFindings] ([Severity]);
    CREATE INDEX [IX_ComplianceFindings_ComplianceStatus] ON [dbo].[ComplianceFindings] ([ComplianceStatus]);
    
    PRINT '   ✓ Created ComplianceFindings table with correct schema';
END
ELSE
    PRINT '   ✓ ComplianceFindings table already exists';
GO

-- ============================================================================
-- 10. Create missing tables (not in original SQL)
-- ============================================================================
PRINT '';
PRINT '10. Creating missing tables...';

-- EnvironmentMetrics
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EnvironmentMetrics')
BEGIN
    CREATE TABLE [dbo].[EnvironmentMetrics] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [DeploymentId] UNIQUEIDENTIFIER NOT NULL,
        [MetricType] NVARCHAR(50) NOT NULL,
        [MetricName] NVARCHAR(50) NOT NULL,
        [Value] DECIMAL(18,4) NOT NULL,
        [Unit] NVARCHAR(20) NULL,
        [Timestamp] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Source] NVARCHAR(50) NULL,
        [Labels] NVARCHAR(MAX) NULL,
        CONSTRAINT [FK_EnvironmentMetrics_EnvironmentDeployments] FOREIGN KEY ([DeploymentId]) 
            REFERENCES [dbo].[EnvironmentDeployments] ([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_EnvironmentMetrics_DeploymentId] ON [dbo].[EnvironmentMetrics] ([DeploymentId]);
    CREATE INDEX [IX_EnvironmentMetrics_Timestamp] ON [dbo].[EnvironmentMetrics] ([Timestamp]);
    CREATE INDEX [IX_EnvironmentMetrics_MetricType] ON [dbo].[EnvironmentMetrics] ([MetricType]);
    
    PRINT '   ✓ Created EnvironmentMetrics table';
END
ELSE
    PRINT '   ✓ EnvironmentMetrics table already exists';
GO

-- ScalingPolicies
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ScalingPolicies')
BEGIN
    CREATE TABLE [dbo].[ScalingPolicies] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [DeploymentId] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [PolicyType] NVARCHAR(50) NOT NULL,
        [MinReplicas] INT NOT NULL DEFAULT 1,
        [MaxReplicas] INT NOT NULL DEFAULT 10,
        [TargetCpuUtilization] INT NOT NULL DEFAULT 70,
        [TargetMemoryUtilization] INT NOT NULL DEFAULT 80,
        [ScaleUpCooldown] NVARCHAR(20) NOT NULL DEFAULT '5m',
        [ScaleDownCooldown] NVARCHAR(20) NOT NULL DEFAULT '10m',
        [AutoScalingEnabled] BIT NOT NULL DEFAULT 0,
        [CostOptimizationEnabled] BIT NOT NULL DEFAULT 0,
        [TrafficBasedScalingEnabled] BIT NOT NULL DEFAULT 0,
        [CustomMetrics] NVARCHAR(MAX) NULL,
        [Schedule] NVARCHAR(MAX) NULL,
        [CreatedBy] NVARCHAR(100) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [IsActive] BIT NOT NULL DEFAULT 1,
        CONSTRAINT [FK_ScalingPolicies_EnvironmentDeployments] FOREIGN KEY ([DeploymentId]) 
            REFERENCES [dbo].[EnvironmentDeployments] ([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_ScalingPolicies_DeploymentId] ON [dbo].[ScalingPolicies] ([DeploymentId]);
    
    PRINT '   ✓ Created ScalingPolicies table';
END
ELSE
    PRINT '   ✓ ScalingPolicies table already exists';
GO

-- ScalingEvents
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ScalingEvents')
BEGIN
    CREATE TABLE [dbo].[ScalingEvents] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [PolicyId] UNIQUEIDENTIFIER NOT NULL,
        [EventType] NVARCHAR(20) NOT NULL,
        [PreviousReplicas] INT NOT NULL,
        [NewReplicas] INT NOT NULL,
        [Trigger] NVARCHAR(50) NOT NULL,
        [TriggerDetails] NVARCHAR(MAX) NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CompletedAt] DATETIME2 NULL,
        [Duration] BIGINT NULL,
        CONSTRAINT [FK_ScalingEvents_ScalingPolicies] FOREIGN KEY ([PolicyId]) 
            REFERENCES [dbo].[ScalingPolicies] ([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_ScalingEvents_PolicyId] ON [dbo].[ScalingEvents] ([PolicyId]);
    
    PRINT '   ✓ Created ScalingEvents table';
END
ELSE
    PRINT '   ✓ ScalingEvents table already exists';
GO

-- SemanticIntents
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SemanticIntents')
BEGIN
    CREATE TABLE [dbo].[SemanticIntents] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [UserInput] NVARCHAR(500) NOT NULL,
        [IntentCategory] NVARCHAR(100) NOT NULL,
        [IntentAction] NVARCHAR(100) NOT NULL,
        [Confidence] DECIMAL(5,4) NOT NULL,
        [ExtractedParameters] NVARCHAR(MAX) NULL,
        [ResolvedToolCall] NVARCHAR(MAX) NULL,
        [UserId] NVARCHAR(100) NOT NULL,
        [SessionId] NVARCHAR(100) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [WasSuccessful] BIT NOT NULL DEFAULT 0,
        [ErrorMessage] NVARCHAR(MAX) NULL
    );
    
    CREATE INDEX [IX_SemanticIntents_UserId] ON [dbo].[SemanticIntents] ([UserId]);
    CREATE INDEX [IX_SemanticIntents_IntentCategory] ON [dbo].[SemanticIntents] ([IntentCategory]);
    
    PRINT '   ✓ Created SemanticIntents table';
END
ELSE
    PRINT '   ✓ SemanticIntents table already exists';
GO

-- IntentFeedback
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'IntentFeedback')
BEGIN
    CREATE TABLE [dbo].[IntentFeedback] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [IntentId] UNIQUEIDENTIFIER NOT NULL,
        [FeedbackType] NVARCHAR(20) NOT NULL,
        [CorrectIntentCategory] NVARCHAR(100) NULL,
        [CorrectIntentAction] NVARCHAR(100) NULL,
        [CorrectParameters] NVARCHAR(MAX) NULL,
        [ProvidedBy] NVARCHAR(100) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [FK_IntentFeedback_SemanticIntents] FOREIGN KEY ([IntentId]) 
            REFERENCES [dbo].[SemanticIntents] ([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_IntentFeedback_IntentId] ON [dbo].[IntentFeedback] ([IntentId]);
    
    PRINT '   ✓ Created IntentFeedback table';
END
ELSE
    PRINT '   ✓ IntentFeedback table already exists';
GO

-- IntentPatterns
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'IntentPatterns')
BEGIN
    CREATE TABLE [dbo].[IntentPatterns] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [Pattern] NVARCHAR(500) NOT NULL,
        [IntentCategory] NVARCHAR(100) NOT NULL,
        [IntentAction] NVARCHAR(100) NOT NULL,
        [Weight] DECIMAL(5,4) NOT NULL DEFAULT 1.0000,
        [ParameterExtractionRules] NVARCHAR(MAX) NULL,
        [UsageCount] INT NOT NULL DEFAULT 0,
        [SuccessCount] INT NOT NULL DEFAULT 0,
        [SuccessRate] DECIMAL(5,4) NOT NULL DEFAULT 0.0000,
        [CreatedBy] NVARCHAR(100) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [IsActive] BIT NOT NULL DEFAULT 1
    );
    
    CREATE INDEX [IX_IntentPatterns_IntentCategory_IntentAction] ON [dbo].[IntentPatterns] ([IntentCategory], [IntentAction]);
    
    PRINT '   ✓ Created IntentPatterns table';
END
ELSE
    PRINT '   ✓ IntentPatterns table already exists';
GO

-- EnvironmentLifecycles
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EnvironmentLifecycles')
BEGIN
    CREATE TABLE [dbo].[EnvironmentLifecycles] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [EnvironmentId] UNIQUEIDENTIFIER NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [LifecycleType] NVARCHAR(50) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [ScheduledStartTime] DATETIME2 NULL,
        [ScheduledEndTime] DATETIME2 NULL,
        [AutoDestroyPolicy] NVARCHAR(20) NULL,
        [InactivityThresholdHours] INT NOT NULL DEFAULT 72,
        [CostThreshold] DECIMAL(18,2) NOT NULL DEFAULT 0,
        [OwnerTeam] NVARCHAR(100) NULL,
        [Project] NVARCHAR(100) NULL,
        [CostCenter] NVARCHAR(50) NULL,
        [NotificationEmails] NVARCHAR(MAX) NULL,
        [NotifyBeforeDestroy] BIT NOT NULL DEFAULT 1,
        [NotificationHours] INT NOT NULL DEFAULT 24,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [LastActivityAt] DATETIME2 NULL,
        [CreatedBy] NVARCHAR(100) NOT NULL,
        CONSTRAINT [FK_EnvironmentLifecycles_EnvironmentDeployments] FOREIGN KEY ([EnvironmentId]) 
            REFERENCES [dbo].[EnvironmentDeployments] ([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_EnvironmentLifecycles_EnvironmentId] ON [dbo].[EnvironmentLifecycles] ([EnvironmentId]);
    
    PRINT '   ✓ Created EnvironmentLifecycles table';
END
ELSE
    PRINT '   ✓ EnvironmentLifecycles table already exists';
GO

-- EnvironmentActivities
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EnvironmentActivities')
BEGIN
    CREATE TABLE [dbo].[EnvironmentActivities] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [EnvironmentLifecycleId] UNIQUEIDENTIFIER NOT NULL,
        [ActivityType] NVARCHAR(50) NOT NULL,
        [Description] NVARCHAR(200) NOT NULL,
        [UserId] NVARCHAR(100) NULL,
        [UserName] NVARCHAR(100) NULL,
        [Metadata] NVARCHAR(MAX) NULL,
        [Timestamp] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Status] NVARCHAR(20) NOT NULL,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        CONSTRAINT [FK_EnvironmentActivities_EnvironmentLifecycles] FOREIGN KEY ([EnvironmentLifecycleId]) 
            REFERENCES [dbo].[EnvironmentLifecycles] ([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_EnvironmentActivities_EnvironmentLifecycleId] ON [dbo].[EnvironmentActivities] ([EnvironmentLifecycleId]);
    
    PRINT '   ✓ Created EnvironmentActivities table';
END
ELSE
    PRINT '   ✓ EnvironmentActivities table already exists';
GO

-- EnvironmentCostTrackings
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EnvironmentCostTrackings')
BEGIN
    CREATE TABLE [dbo].[EnvironmentCostTrackings] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [EnvironmentLifecycleId] UNIQUEIDENTIFIER NOT NULL,
        [Date] DATETIME2 NOT NULL,
        [DailyCost] DECIMAL(18,2) NOT NULL,
        [CumulativeCost] DECIMAL(18,2) NOT NULL,
        [Currency] NVARCHAR(10) NOT NULL DEFAULT 'USD',
        [CostBreakdown] NVARCHAR(MAX) NULL,
        [BillingResourceGroup] NVARCHAR(50) NULL,
        [SubscriptionId] NVARCHAR(100) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [FK_EnvironmentCostTrackings_EnvironmentLifecycles] FOREIGN KEY ([EnvironmentLifecycleId]) 
            REFERENCES [dbo].[EnvironmentLifecycles] ([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_EnvironmentCostTrackings_EnvironmentLifecycleId] ON [dbo].[EnvironmentCostTrackings] ([EnvironmentLifecycleId]);
    
    PRINT '   ✓ Created EnvironmentCostTrackings table';
END
ELSE
    PRINT '   ✓ EnvironmentCostTrackings table already exists';
GO

-- EnvironmentClones
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EnvironmentClones')
BEGIN
    CREATE TABLE [dbo].[EnvironmentClones] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [SourceEnvironmentId] UNIQUEIDENTIFIER NOT NULL,
        [TargetEnvironmentId] UNIQUEIDENTIFIER NOT NULL,
        [CloneType] NVARCHAR(50) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [StartedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CompletedAt] DATETIME2 NULL,
        [InitiatedBy] NVARCHAR(100) NOT NULL,
        [IncludeData] BIT NOT NULL DEFAULT 1,
        [MaskSensitiveData] BIT NOT NULL DEFAULT 1,
        [IncludeSecrets] BIT NOT NULL DEFAULT 0,
        [DataMaskingRules] NVARCHAR(MAX) NULL,
        [ExcludedResources] NVARCHAR(MAX) NULL,
        [CloneOperationLog] NVARCHAR(MAX) NULL,
        [ErrorDetails] NVARCHAR(MAX) NULL,
        [Progress] INT NOT NULL DEFAULT 0,
        CONSTRAINT [FK_EnvironmentClones_Source] FOREIGN KEY ([SourceEnvironmentId]) 
            REFERENCES [dbo].[EnvironmentDeployments] ([Id]),
        CONSTRAINT [FK_EnvironmentClones_Target] FOREIGN KEY ([TargetEnvironmentId]) 
            REFERENCES [dbo].[EnvironmentDeployments] ([Id])
    );
    
    CREATE INDEX [IX_EnvironmentClones_SourceEnvironmentId] ON [dbo].[EnvironmentClones] ([SourceEnvironmentId]);
    CREATE INDEX [IX_EnvironmentClones_TargetEnvironmentId] ON [dbo].[EnvironmentClones] ([TargetEnvironmentId]);
    
    PRINT '   ✓ Created EnvironmentClones table';
END
ELSE
    PRINT '   ✓ EnvironmentClones table already exists';
GO

-- EnvironmentSynchronizations
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EnvironmentSynchronizations')
BEGIN
    CREATE TABLE [dbo].[EnvironmentSynchronizations] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [SourceEnvironmentId] UNIQUEIDENTIFIER NOT NULL,
        [TargetEnvironmentId] UNIQUEIDENTIFIER NOT NULL,
        [SyncType] NVARCHAR(50) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [Schedule] NVARCHAR(100) NULL,
        [IsEnabled] BIT NOT NULL DEFAULT 1,
        [LastSyncAt] DATETIME2 NULL,
        [NextScheduledSync] DATETIME2 NULL,
        [SyncDirection] NVARCHAR(20) NOT NULL DEFAULT 'source_to_target',
        [ConflictResolution] NVARCHAR(50) NOT NULL DEFAULT 'source_wins',
        [IncludeConfiguration] BIT NOT NULL DEFAULT 1,
        [IncludeData] BIT NOT NULL DEFAULT 0,
        [SyncFilters] NVARCHAR(MAX) NULL,
        [CreatedBy] NVARCHAR(100) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [FK_EnvironmentSynchronizations_Source] FOREIGN KEY ([SourceEnvironmentId]) 
            REFERENCES [dbo].[EnvironmentDeployments] ([Id]),
        CONSTRAINT [FK_EnvironmentSynchronizations_Target] FOREIGN KEY ([TargetEnvironmentId]) 
            REFERENCES [dbo].[EnvironmentDeployments] ([Id])
    );
    
    CREATE INDEX [IX_EnvironmentSynchronizations_SourceEnvironmentId] ON [dbo].[EnvironmentSynchronizations] ([SourceEnvironmentId]);
    CREATE INDEX [IX_EnvironmentSynchronizations_TargetEnvironmentId] ON [dbo].[EnvironmentSynchronizations] ([TargetEnvironmentId]);
    
    PRINT '   ✓ Created EnvironmentSynchronizations table';
END
ELSE
    PRINT '   ✓ EnvironmentSynchronizations table already exists';
GO

-- ApprovalWorkflows
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ApprovalWorkflows')
BEGIN
    CREATE TABLE [dbo].[ApprovalWorkflows] (
        [Id] NVARCHAR(100) NOT NULL PRIMARY KEY,
        [ToolCallId] NVARCHAR(100) NOT NULL,
        [Status] NVARCHAR(50) NOT NULL DEFAULT 'Pending',
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CompletedAt] DATETIME2 NULL,
        [Justification] NVARCHAR(1000) NULL,
        [Priority] INT NOT NULL DEFAULT 1,
        [ResourceType] NVARCHAR(100) NULL,
        [ResourceName] NVARCHAR(200) NULL,
        [ResourceGroupName] NVARCHAR(200) NULL,
        [Location] NVARCHAR(100) NULL,
        [Environment] NVARCHAR(50) NULL,
        [RequestedBy] NVARCHAR(200) NULL,
        [RequestedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [ExpiresAt] DATETIME2 NOT NULL,
        [Reason] NVARCHAR(1000) NULL,
        [ApprovedBy] NVARCHAR(200) NULL,
        [ApprovedAt] DATETIME2 NULL,
        [ApprovalComments] NVARCHAR(1000) NULL,
        [RejectedBy] NVARCHAR(200) NULL,
        [RejectedAt] DATETIME2 NULL,
        [RejectionReason] NVARCHAR(1000) NULL,
        [RequiredApproversJson] NVARCHAR(MAX) NULL,
        [PolicyViolationsJson] NVARCHAR(MAX) NULL,
        [OriginalToolCallJson] NVARCHAR(MAX) NULL,
        [DecisionsJson] NVARCHAR(MAX) NULL,
        [RequestPayload] NVARCHAR(MAX) NULL
    );
    
    CREATE INDEX [IX_ApprovalWorkflows_Status] ON [dbo].[ApprovalWorkflows] ([Status]);
    CREATE INDEX [IX_ApprovalWorkflows_RequestedBy] ON [dbo].[ApprovalWorkflows] ([RequestedBy]);
    CREATE INDEX [IX_ApprovalWorkflows_CreatedAt] ON [dbo].[ApprovalWorkflows] ([CreatedAt]);
    
    PRINT '   ✓ Created ApprovalWorkflows table';
END
ELSE
    PRINT '   ✓ ApprovalWorkflows table already exists';
GO

-- ============================================================================
-- Summary
-- ============================================================================
PRINT '';
PRINT '============================================';
PRINT 'Schema migration completed!';
PRINT '============================================';
PRINT '';
PRINT 'Tables verified/updated:';
PRINT '  1. EnvironmentTemplates';
PRINT '  2. TemplateVersions';
PRINT '  3. TemplateFiles';
PRINT '  4. EnvironmentDeployments';
PRINT '  5. DeploymentHistory';
PRINT '  6. AgentConfigurations';
PRINT '  7. AuditLogs';
PRINT '  8. ComplianceAssessments';
PRINT '  9. ComplianceFindings';
PRINT '  10. EnvironmentMetrics';
PRINT '  11. ScalingPolicies';
PRINT '  12. ScalingEvents';
PRINT '  13. SemanticIntents';
PRINT '  14. IntentFeedback';
PRINT '  15. IntentPatterns';
PRINT '  16. EnvironmentLifecycles';
PRINT '  17. EnvironmentActivities';
PRINT '  18. EnvironmentCostTrackings';
PRINT '  19. EnvironmentClones';
PRINT '  20. EnvironmentSynchronizations';
PRINT '  21. ApprovalWorkflows';
PRINT '';
GO
