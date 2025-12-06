namespace Platform.Engineering.Copilot.Core.Constants;

/// <summary>
/// Constants used across compliance services.
/// Centralizes magic strings for consistency and maintainability.
/// </summary>
public static class ComplianceConstants
{
    #region Compliance Frameworks

    /// <summary>
    /// Supported compliance framework identifiers.
    /// </summary>
    public static class Frameworks
    {
        public const string Nist80053 = "NIST80053";
        public const string Nist80053Rev5 = "NIST80053Rev5";
        public const string FedRampHigh = "FedRAMPHigh";
        public const string FedRampModerate = "FedRAMPModerate";
        public const string FedRampLow = "FedRAMPLow";
        public const string DoDIL5 = "DoD IL5";
        public const string DoDIL4 = "DoD IL4";
        public const string Soc2 = "SOC2";
        public const string Gdpr = "GDPR";
        public const string Hipaa = "HIPAA";
        public const string PciDss = "PCI-DSS";
        public const string CisAzure = "CIS-Azure";
    }

    #endregion

    #region NIST Control Families

    /// <summary>
    /// NIST 800-53 control family codes.
    /// </summary>
    public static class ControlFamilies
    {
        public const string AccessControl = "AC";
        public const string AwarenessTraining = "AT";
        public const string AuditAccountability = "AU";
        public const string SecurityAssessment = "CA";
        public const string ConfigurationManagement = "CM";
        public const string ContingencyPlanning = "CP";
        public const string IdentificationAuthentication = "IA";
        public const string IncidentResponse = "IR";
        public const string Maintenance = "MA";
        public const string MediaProtection = "MP";
        public const string PhysicalEnvironmental = "PE";
        public const string Planning = "PL";
        public const string ProgramManagement = "PM";
        public const string PersonnelSecurity = "PS";
        public const string RiskAssessment = "RA";
        public const string SystemServicesAcquisition = "SA";
        public const string SystemCommunications = "SC";
        public const string SystemInformationIntegrity = "SI";

        // Full name aliases for clarity
        public const string SystemCommunicationsProtection = SystemCommunications;
        public const string SystemAndCommunicationsProtection = SystemCommunications;

        /// <summary>
        /// All control family codes.
        /// </summary>
        public static readonly IReadOnlyList<string> All = new[]
        {
            AccessControl, AwarenessTraining, AuditAccountability, SecurityAssessment,
            ConfigurationManagement, ContingencyPlanning, IdentificationAuthentication,
            IncidentResponse, Maintenance, MediaProtection, PhysicalEnvironmental,
            Planning, ProgramManagement, PersonnelSecurity, RiskAssessment,
            SystemServicesAcquisition, SystemCommunications, SystemInformationIntegrity
        };
    }

    #endregion

    #region Severity Levels

    /// <summary>
    /// Finding severity level constants.
    /// </summary>
    public static class Severity
    {
        public const string Critical = "Critical";
        public const string High = "High";
        public const string Medium = "Medium";
        public const string Low = "Low";
        public const string Informational = "Informational";

        /// <summary>
        /// Severity levels in priority order (highest first).
        /// </summary>
        public static readonly IReadOnlyList<string> PriorityOrder = new[]
        {
            Critical, High, Medium, Low, Informational
        };
    }

    #endregion

    #region Compliance Status

    /// <summary>
    /// Compliance status values.
    /// </summary>
    public static class Status
    {
        public const string Compliant = "Compliant";
        public const string NonCompliant = "NonCompliant";
        public const string PartiallyCompliant = "PartiallyCompliant";
        public const string NotApplicable = "NotApplicable";
        public const string Unknown = "Unknown";
        public const string Inherited = "Inherited";
        public const string Planned = "Planned";
    }

    #endregion

    #region Remediation Status

    /// <summary>
    /// Remediation execution status values.
    /// </summary>
    public static class RemediationStatus
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string InProgress = "InProgress";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Validating = "Validating";
        public const string RolledBack = "RolledBack";
    }

    #endregion

    #region Azure Resource Types

    /// <summary>
    /// Common Azure resource type identifiers.
    /// </summary>
    public static class AzureResourceTypes
    {
        // Singular names (original)
        public const string PolicyAssignment = "Microsoft.Authorization/policyAssignments";
        public const string DiagnosticSettings = "Microsoft.Insights/diagnosticSettings";
        public const string NetworkSecurityGroup = "Microsoft.Network/networkSecurityGroups";
        public const string VirtualMachine = "Microsoft.Compute/virtualMachines";
        public const string StorageAccount = "Microsoft.Storage/storageAccounts";
        public const string KeyVault = "Microsoft.KeyVault/vaults";
        public const string SqlServer = "Microsoft.Sql/servers";
        public const string SqlDatabase = "Microsoft.Sql/servers/databases";
        public const string AppService = "Microsoft.Web/sites";
        public const string FunctionApp = "Microsoft.Web/sites"; // Same as AppService with kind=functionapp
        public const string AksCluster = "Microsoft.ContainerService/managedClusters";
        public const string VirtualNetwork = "Microsoft.Network/virtualNetworks";
        public const string LoadBalancer = "Microsoft.Network/loadBalancers";
        public const string ApplicationGateway = "Microsoft.Network/applicationGateways";
        public const string LogAnalyticsWorkspace = "Microsoft.OperationalInsights/workspaces";
        public const string SecurityContact = "Microsoft.Security/securityContacts";
        public const string ResourceGroup = "Microsoft.Resources/resourceGroups";
        public const string Subscription = "Subscription";
        public const string CosmosDb = "Microsoft.DocumentDB/databaseAccounts";
        public const string ServiceBus = "Microsoft.ServiceBus/namespaces";
        public const string EventHub = "Microsoft.EventHub/namespaces";
        public const string RedisCache = "Microsoft.Cache/Redis";
        public const string ContainerRegistry = "Microsoft.ContainerRegistry/registries";
        public const string AzureFirewall = "Microsoft.Network/azureFirewalls";

        // Plural aliases for convenience and consistency with resource type naming
        public const string PolicyAssignments = PolicyAssignment;
        public const string NetworkSecurityGroups = NetworkSecurityGroup;
        public const string VirtualMachines = VirtualMachine;
        public const string StorageAccounts = StorageAccount;
        public const string KeyVaults = KeyVault;
        public const string SqlServers = SqlServer;
        public const string SqlDatabases = SqlDatabase;
        public const string VirtualNetworks = VirtualNetwork;
        public const string LoadBalancers = LoadBalancer;
        public const string ApplicationGateways = ApplicationGateway;
        public const string AzureFirewalls = AzureFirewall;
    }

    #endregion

    #region Finding Types

    /// <summary>
    /// Types of compliance findings.
    /// </summary>
    public static class FindingTypes
    {
        public const string Encryption = "Encryption";
        public const string NetworkSecurity = "NetworkSecurity";
        public const string AccessControl = "AccessControl";
        public const string Configuration = "Configuration";
        public const string Logging = "Logging";
        public const string Monitoring = "Monitoring";
        public const string DataProtection = "DataProtection";
        public const string IdentityManagement = "IdentityManagement";
        public const string Vulnerability = "Vulnerability";
        public const string Misconfiguration = "Misconfiguration";
    }

    #endregion

    #region Script Types

    /// <summary>
    /// Supported remediation script types.
    /// </summary>
    public static class ScriptTypes
    {
        public const string AzureCli = "AzureCLI";
        public const string PowerShell = "PowerShell";
        public const string Terraform = "Terraform";
        public const string Bicep = "Bicep";
        public const string ArmTemplate = "ARMTemplate";
    }

    #endregion

    #region Cache Keys

    /// <summary>
    /// Cache key prefixes for compliance data.
    /// </summary>
    public static class CacheKeys
    {
        public const string NistCatalogPrefix = "nist:catalog:";
        public const string NistControlPrefix = "nist:control:";
        public const string NistFamilyPrefix = "nist:family:";
        public const string AssessmentPrefix = "compliance:assessment:";
        public const string FindingsPrefix = "compliance:findings:";
        public const string RemediationPlanPrefix = "compliance:remediation:";
        public const string EvidencePrefix = "compliance:evidence:";
    }

    #endregion

    #region Configuration Keys

    /// <summary>
    /// Configuration section names.
    /// </summary>
    public static class ConfigSections
    {
        public const string ComplianceAgent = "ComplianceAgent";
        public const string NistControls = "NistControls";
        public const string CodeScanning = "CodeScanning";
        public const string EvidenceStorage = "EvidenceStorage";
        public const string AuditLogging = "AuditLogging";
        public const string Governance = "Governance";
    }

    #endregion

    #region Logging Event IDs

    /// <summary>
    /// Event IDs for structured logging.
    /// </summary>
    public static class LogEvents
    {
        // Assessment Events (1xxx)
        public const int AssessmentStarted = 1001;
        public const int AssessmentCompleted = 1002;
        public const int AssessmentFailed = 1003;
        public const int ControlEvaluated = 1010;
        public const int FindingCreated = 1020;

        // Remediation Events (2xxx)
        public const int RemediationStarted = 2001;
        public const int RemediationCompleted = 2002;
        public const int RemediationFailed = 2003;
        public const int RemediationRolledBack = 2004;
        public const int RemediationApproved = 2010;
        public const int RemediationRejected = 2011;

        // Evidence Events (3xxx)
        public const int EvidenceCollected = 3001;
        public const int EvidenceStored = 3002;
        public const int EvidenceRetrieved = 3003;
        public const int EvidenceExpired = 3010;

        // Scanning Events (4xxx)
        public const int ScanStarted = 4001;
        public const int ScanCompleted = 4002;
        public const int ScanFailed = 4003;
        public const int VulnerabilityFound = 4010;

        // Integration Events (5xxx)
        public const int NistControlsFetched = 5001;
        public const int AzureResourceScanned = 5010;
        public const int DefenderAlertReceived = 5020;
    }

    #endregion

    #region Default Values

    /// <summary>
    /// Default configuration values.
    /// </summary>
    public static class Defaults
    {
        public const int CacheExpirationMinutes = 60;
        public const int MaxConcurrentRemediations = 5;
        public const int ScriptTimeoutSeconds = 300;
        public const int MaxRetryAttempts = 3;
        public const int EvidenceRetentionDays = 365;
        public const int LogRetentionDays = 90;
        public const double MinComplianceScoreThreshold = 70.0;
    }

    #endregion

    #region Correlation ID Headers

    /// <summary>
    /// HTTP header names for correlation tracking.
    /// </summary>
    public static class Headers
    {
        public const string CorrelationId = "X-Correlation-ID";
        public const string RequestId = "X-Request-ID";
        public const string SessionId = "X-Session-ID";
        public const string UserId = "X-User-ID";
        public const string SubscriptionId = "X-Subscription-ID";
    }

    #endregion
}
