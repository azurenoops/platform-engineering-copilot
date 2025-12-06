-- Migration Script: Update EnvironmentDeployments table to match EF Core entity schema
-- Run this script in Azure SQL Database Query Editor
-- This adds missing columns required by the EnvironmentDeployment entity

PRINT 'Starting EnvironmentDeployments schema migration...';

-- Check if EnvironmentDeployments table exists
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'EnvironmentDeployments')
BEGIN
    PRINT 'EnvironmentDeployments table exists, checking for missing columns...';

    -- Rename DeploymentParameters to Parameters if it exists
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'DeploymentParameters')
    BEGIN
        EXEC sp_rename 'EnvironmentDeployments.DeploymentParameters', 'Parameters', 'COLUMN';
        PRINT 'Renamed DeploymentParameters to Parameters';
    END

    -- Rename DeploymentOutputs to Configuration if it exists  
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'DeploymentOutputs')
    BEGIN
        EXEC sp_rename 'EnvironmentDeployments.DeploymentOutputs', 'Configuration', 'COLUMN';
        PRINT 'Renamed DeploymentOutputs to Configuration';
    END

    -- Add Configuration column if it doesn't exist (and wasn't renamed)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'Configuration')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [Configuration] NVARCHAR(MAX) NULL;
        PRINT 'Added Configuration column';
    END

    -- Add Parameters column if it doesn't exist (and wasn't renamed)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'Parameters')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [Parameters] NVARCHAR(MAX) NULL;
        PRINT 'Added Parameters column';
    END

    -- Add Tags column
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'Tags')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [Tags] NVARCHAR(MAX) NULL;
        PRINT 'Added Tags column';
    END

    -- Add DeployedBy column
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'DeployedBy')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [DeployedBy] NVARCHAR(100) NOT NULL DEFAULT 'system';
        PRINT 'Added DeployedBy column';
    END

    -- Add IsPollingActive column
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'IsPollingActive')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [IsPollingActive] BIT NOT NULL DEFAULT 0;
        PRINT 'Added IsPollingActive column';
    END

    -- Add LastPolledAt column
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'LastPolledAt')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [LastPolledAt] DATETIME2 NULL;
        PRINT 'Added LastPolledAt column';
    END

    -- Add PollingAttempts column
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'PollingAttempts')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [PollingAttempts] INT NOT NULL DEFAULT 0;
        PRINT 'Added PollingAttempts column';
    END

    -- Add CurrentPollingInterval column (stored as bigint ticks)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'CurrentPollingInterval')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [CurrentPollingInterval] BIGINT NULL;
        PRINT 'Added CurrentPollingInterval column';
    END

    -- Add ProgressPercentage column
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'ProgressPercentage')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [ProgressPercentage] INT NOT NULL DEFAULT 0;
        PRINT 'Added ProgressPercentage column';
    END

    -- Add EstimatedTimeRemaining column (stored as bigint ticks)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'EstimatedTimeRemaining')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [EstimatedTimeRemaining] BIGINT NULL;
        PRINT 'Added EstimatedTimeRemaining column';
    END

    -- Add EstimatedMonthlyCost column
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'EstimatedMonthlyCost')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [EstimatedMonthlyCost] DECIMAL(10,2) NULL;
        PRINT 'Added EstimatedMonthlyCost column';
    END

    -- Add ActualMonthlyCost column
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EnvironmentDeployments') AND name = 'ActualMonthlyCost')
    BEGIN
        ALTER TABLE [dbo].[EnvironmentDeployments] ADD [ActualMonthlyCost] DECIMAL(10,2) NULL;
        PRINT 'Added ActualMonthlyCost column';
    END

    PRINT 'EnvironmentDeployments schema migration completed.';
END
ELSE
BEGIN
    PRINT 'EnvironmentDeployments table does not exist. Please run create-platform-tables.sql first.';
END
GO

-- Update DeploymentHistory table
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DeploymentHistory')
BEGIN
    PRINT 'Checking DeploymentHistory table for missing columns...';

    -- Add Duration column (stored as bigint ticks)
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('DeploymentHistory') AND name = 'Duration')
    BEGIN
        ALTER TABLE [dbo].[DeploymentHistory] ADD [Duration] BIGINT NULL;
        PRINT 'Added Duration column to DeploymentHistory';
    END

    PRINT 'DeploymentHistory schema migration completed.';
END
GO

PRINT 'All schema migrations completed successfully!';
GO
