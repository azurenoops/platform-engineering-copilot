-- Platform Engineering Copilot - Create Missing Tables
-- Run this script in Azure SQL Database Query Editor to create the template-related tables
-- These tables are used by the Infrastructure Agent to persist generated templates

-- EnvironmentTemplates table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EnvironmentTemplates')
BEGIN
    CREATE TABLE [dbo].[EnvironmentTemplates] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [Name] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NOT NULL,
        [TemplateType] NVARCHAR(50) NOT NULL,
        [Version] NVARCHAR(20) NOT NULL,
        [Content] NVARCHAR(MAX) NOT NULL,
        [Format] NVARCHAR(50) NOT NULL,
        [DeploymentTier] NVARCHAR(20) NOT NULL,
        [MultiRegionSupported] BIT NOT NULL DEFAULT 0,
        [DisasterRecoverySupported] BIT NOT NULL DEFAULT 0,
        [HighAvailabilitySupported] BIT NOT NULL DEFAULT 0,
        [Parameters] NVARCHAR(MAX) NULL,
        [Tags] NVARCHAR(MAX) NULL,
        [CreatedBy] NVARCHAR(100) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [IsActive] BIT NOT NULL DEFAULT 1,
        [IsPublic] BIT NOT NULL DEFAULT 0,
        [AzureService] NVARCHAR(50) NULL,
        [AutoScalingEnabled] BIT NOT NULL DEFAULT 0,
        [MonitoringEnabled] BIT NOT NULL DEFAULT 1,
        [BackupEnabled] BIT NOT NULL DEFAULT 0,
        [FilesCount] INT NOT NULL DEFAULT 0,
        [MainFileType] NVARCHAR(50) NULL,
        [Summary] NVARCHAR(MAX) NULL
    );
    
    CREATE UNIQUE INDEX [IX_EnvironmentTemplates_Name] ON [dbo].[EnvironmentTemplates] ([Name]);
    CREATE INDEX [IX_EnvironmentTemplates_TemplateType_DeploymentTier] ON [dbo].[EnvironmentTemplates] ([TemplateType], [DeploymentTier]);
    CREATE INDEX [IX_EnvironmentTemplates_CreatedAt] ON [dbo].[EnvironmentTemplates] ([CreatedAt]);
    CREATE INDEX [IX_EnvironmentTemplates_IsActive] ON [dbo].[EnvironmentTemplates] ([IsActive]);
    
    PRINT 'Created EnvironmentTemplates table';
END
ELSE
    PRINT 'EnvironmentTemplates table already exists';
GO

-- TemplateVersions table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TemplateVersions')
BEGIN
    CREATE TABLE [dbo].[TemplateVersions] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [TemplateId] UNIQUEIDENTIFIER NOT NULL,
        [Version] NVARCHAR(20) NOT NULL,
        [Content] NVARCHAR(MAX) NOT NULL,
        [ChangeLog] NVARCHAR(MAX) NULL,
        [CreatedBy] NVARCHAR(100) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [IsDeprecated] BIT NOT NULL DEFAULT 0,
        CONSTRAINT [FK_TemplateVersions_EnvironmentTemplates] FOREIGN KEY ([TemplateId]) 
            REFERENCES [dbo].[EnvironmentTemplates] ([Id]) ON DELETE CASCADE
    );
    
    CREATE UNIQUE INDEX [IX_TemplateVersions_TemplateId_Version] ON [dbo].[TemplateVersions] ([TemplateId], [Version]);
    CREATE INDEX [IX_TemplateVersions_CreatedAt] ON [dbo].[TemplateVersions] ([CreatedAt]);
    
    PRINT 'Created TemplateVersions table';
END
ELSE
    PRINT 'TemplateVersions table already exists';
GO

-- TemplateFiles table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TemplateFiles')
BEGIN
    CREATE TABLE [dbo].[TemplateFiles] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [TemplateId] UNIQUEIDENTIFIER NOT NULL,
        [FileName] NVARCHAR(255) NOT NULL,
        [FilePath] NVARCHAR(500) NOT NULL,
        [Content] NVARCHAR(MAX) NOT NULL,
        [FileType] NVARCHAR(50) NOT NULL,
        [IsEntryPoint] BIT NOT NULL DEFAULT 0,
        [Order] INT NOT NULL DEFAULT 0,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [FK_TemplateFiles_EnvironmentTemplates] FOREIGN KEY ([TemplateId]) 
            REFERENCES [dbo].[EnvironmentTemplates] ([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_TemplateFiles_TemplateId] ON [dbo].[TemplateFiles] ([TemplateId]);
    
    PRINT 'Created TemplateFiles table';
END
ELSE
    PRINT 'TemplateFiles table already exists';
GO

-- EnvironmentDeployments table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EnvironmentDeployments')
BEGIN
    CREATE TABLE [dbo].[EnvironmentDeployments] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [Name] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [EnvironmentType] NVARCHAR(50) NOT NULL,
        [Status] NVARCHAR(50) NOT NULL,
        [TemplateId] UNIQUEIDENTIFIER NULL,
        [ResourceGroupName] NVARCHAR(100) NOT NULL,
        [SubscriptionId] NVARCHAR(100) NOT NULL,
        [Location] NVARCHAR(50) NOT NULL,
        [DeploymentParameters] NVARCHAR(MAX) NULL,
        [DeploymentOutputs] NVARCHAR(MAX) NULL,
        [CreatedBy] NVARCHAR(100) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [IsDeleted] BIT NOT NULL DEFAULT 0,
        [DeletedAt] DATETIME2 NULL,
        [DeletedBy] NVARCHAR(100) NULL,
        CONSTRAINT [FK_EnvironmentDeployments_EnvironmentTemplates] FOREIGN KEY ([TemplateId]) 
            REFERENCES [dbo].[EnvironmentTemplates] ([Id])
    );
    
    CREATE INDEX [IX_EnvironmentDeployments_Name] ON [dbo].[EnvironmentDeployments] ([Name]);
    CREATE INDEX [IX_EnvironmentDeployments_EnvironmentType_Status] ON [dbo].[EnvironmentDeployments] ([EnvironmentType], [Status]);
    CREATE INDEX [IX_EnvironmentDeployments_ResourceGroupName] ON [dbo].[EnvironmentDeployments] ([ResourceGroupName]);
    CREATE INDEX [IX_EnvironmentDeployments_SubscriptionId] ON [dbo].[EnvironmentDeployments] ([SubscriptionId]);
    CREATE INDEX [IX_EnvironmentDeployments_CreatedAt] ON [dbo].[EnvironmentDeployments] ([CreatedAt]);
    CREATE INDEX [IX_EnvironmentDeployments_IsDeleted] ON [dbo].[EnvironmentDeployments] ([IsDeleted]);
    
    PRINT 'Created EnvironmentDeployments table';
END
ELSE
    PRINT 'EnvironmentDeployments table already exists';
GO

-- DeploymentHistory table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DeploymentHistory')
BEGIN
    CREATE TABLE [dbo].[DeploymentHistory] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [DeploymentId] UNIQUEIDENTIFIER NOT NULL,
        [Action] NVARCHAR(50) NOT NULL,
        [Status] NVARCHAR(50) NOT NULL,
        [StartedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CompletedAt] DATETIME2 NULL,
        [InitiatedBy] NVARCHAR(100) NOT NULL,
        [Details] NVARCHAR(MAX) NULL,
        [ErrorMessage] NVARCHAR(MAX) NULL,
        CONSTRAINT [FK_DeploymentHistory_EnvironmentDeployments] FOREIGN KEY ([DeploymentId]) 
            REFERENCES [dbo].[EnvironmentDeployments] ([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_DeploymentHistory_DeploymentId_StartedAt] ON [dbo].[DeploymentHistory] ([DeploymentId], [StartedAt]);
    CREATE INDEX [IX_DeploymentHistory_Action] ON [dbo].[DeploymentHistory] ([Action]);
    CREATE INDEX [IX_DeploymentHistory_Status] ON [dbo].[DeploymentHistory] ([Status]);
    
    PRINT 'Created DeploymentHistory table';
END
ELSE
    PRINT 'DeploymentHistory table already exists';
GO

-- AgentConfigurations table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AgentConfigurations')
BEGIN
    CREATE TABLE [dbo].[AgentConfigurations] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [AgentType] NVARCHAR(50) NOT NULL,
        [ConfigKey] NVARCHAR(100) NOT NULL,
        [ConfigValue] NVARCHAR(MAX) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [IsEnabled] BIT NOT NULL DEFAULT 1,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] NVARCHAR(100) NULL,
        [UpdatedBy] NVARCHAR(100) NULL
    );
    
    CREATE UNIQUE INDEX [IX_AgentConfigurations_AgentType_ConfigKey] ON [dbo].[AgentConfigurations] ([AgentType], [ConfigKey]);
    
    PRINT 'Created AgentConfigurations table';
END
ELSE
    PRINT 'AgentConfigurations table already exists';
GO

-- AuditLogs table (NIST 800-53 AU-2, AU-3, AU-9)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
BEGIN
    CREATE TABLE [dbo].[AuditLogs] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [Timestamp] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [EventType] NVARCHAR(100) NOT NULL,
        [EventCategory] NVARCHAR(50) NOT NULL,
        [UserId] NVARCHAR(100) NULL,
        [UserEmail] NVARCHAR(255) NULL,
        [SourceIp] NVARCHAR(50) NULL,
        [ResourceType] NVARCHAR(100) NULL,
        [ResourceId] NVARCHAR(500) NULL,
        [Action] NVARCHAR(100) NOT NULL,
        [Outcome] NVARCHAR(50) NOT NULL,
        [Details] NVARCHAR(MAX) NULL,
        [CorrelationId] NVARCHAR(100) NULL,
        [SessionId] NVARCHAR(100) NULL
    );
    
    CREATE INDEX [IX_AuditLogs_Timestamp] ON [dbo].[AuditLogs] ([Timestamp]);
    CREATE INDEX [IX_AuditLogs_EventType] ON [dbo].[AuditLogs] ([EventType]);
    CREATE INDEX [IX_AuditLogs_UserId] ON [dbo].[AuditLogs] ([UserId]);
    CREATE INDEX [IX_AuditLogs_CorrelationId] ON [dbo].[AuditLogs] ([CorrelationId]);
    
    PRINT 'Created AuditLogs table';
END
ELSE
    PRINT 'AuditLogs table already exists';
GO

-- ComplianceAssessments table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ComplianceAssessments')
BEGIN
    CREATE TABLE [dbo].[ComplianceAssessments] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [Name] NVARCHAR(200) NOT NULL,
        [Framework] NVARCHAR(50) NOT NULL,
        [Scope] NVARCHAR(500) NOT NULL,
        [SubscriptionId] NVARCHAR(100) NULL,
        [ResourceGroupName] NVARCHAR(100) NULL,
        [Status] NVARCHAR(50) NOT NULL,
        [OverallScore] DECIMAL(5,2) NULL,
        [TotalControls] INT NOT NULL DEFAULT 0,
        [PassedControls] INT NOT NULL DEFAULT 0,
        [FailedControls] INT NOT NULL DEFAULT 0,
        [NotApplicableControls] INT NOT NULL DEFAULT 0,
        [StartedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CompletedAt] DATETIME2 NULL,
        [InitiatedBy] NVARCHAR(100) NOT NULL,
        [Summary] NVARCHAR(MAX) NULL
    );
    
    CREATE INDEX [IX_ComplianceAssessments_Framework] ON [dbo].[ComplianceAssessments] ([Framework]);
    CREATE INDEX [IX_ComplianceAssessments_Status] ON [dbo].[ComplianceAssessments] ([Status]);
    CREATE INDEX [IX_ComplianceAssessments_StartedAt] ON [dbo].[ComplianceAssessments] ([StartedAt]);
    
    PRINT 'Created ComplianceAssessments table';
END
ELSE
    PRINT 'ComplianceAssessments table already exists';
GO

-- ComplianceFindings table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ComplianceFindings')
BEGIN
    CREATE TABLE [dbo].[ComplianceFindings] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [AssessmentId] UNIQUEIDENTIFIER NOT NULL,
        [ControlId] NVARCHAR(50) NOT NULL,
        [ControlFamily] NVARCHAR(50) NOT NULL,
        [ControlName] NVARCHAR(200) NOT NULL,
        [Severity] NVARCHAR(20) NOT NULL,
        [Status] NVARCHAR(50) NOT NULL,
        [ResourceType] NVARCHAR(100) NULL,
        [ResourceId] NVARCHAR(500) NULL,
        [ResourceName] NVARCHAR(200) NULL,
        [Finding] NVARCHAR(MAX) NOT NULL,
        [Recommendation] NVARCHAR(MAX) NULL,
        [RemediationSteps] NVARCHAR(MAX) NULL,
        [Evidence] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT [FK_ComplianceFindings_ComplianceAssessments] FOREIGN KEY ([AssessmentId]) 
            REFERENCES [dbo].[ComplianceAssessments] ([Id]) ON DELETE CASCADE
    );
    
    CREATE INDEX [IX_ComplianceFindings_AssessmentId] ON [dbo].[ComplianceFindings] ([AssessmentId]);
    CREATE INDEX [IX_ComplianceFindings_ControlId] ON [dbo].[ComplianceFindings] ([ControlId]);
    CREATE INDEX [IX_ComplianceFindings_Severity] ON [dbo].[ComplianceFindings] ([Severity]);
    CREATE INDEX [IX_ComplianceFindings_Status] ON [dbo].[ComplianceFindings] ([Status]);
    
    PRINT 'Created ComplianceFindings table';
END
ELSE
    PRINT 'ComplianceFindings table already exists';
GO

PRINT '';
PRINT '========================================';
PRINT 'Platform Engineering Copilot tables created successfully!';
PRINT '========================================';
