-- =============================================================================
-- Feature 007: Remove ATO Compliance Engine
-- Drop compliance tables from the Platform Engineering Copilot database
-- 
-- IDEMPOTENT: Safe to run multiple times.
-- ORDER: Child tables first to respect FK constraints.
-- APPLY: After deploying code changes (DbSets removed from DbContext).
-- =============================================================================

-- Drop child tables first (reference ComplianceAssessments via FK)
IF OBJECT_ID('dbo.EvidencePackages', 'U') IS NOT NULL
    DROP TABLE dbo.EvidencePackages;

IF OBJECT_ID('dbo.ComplianceDocuments', 'U') IS NOT NULL
    DROP TABLE dbo.ComplianceDocuments;

IF OBJECT_ID('dbo.ComplianceFindings', 'U') IS NOT NULL
    DROP TABLE dbo.ComplianceFindings;

-- Drop parent table last
IF OBJECT_ID('dbo.ComplianceAssessments', 'U') IS NOT NULL
    DROP TABLE dbo.ComplianceAssessments;

PRINT 'Feature 007: Compliance tables dropped successfully.';
