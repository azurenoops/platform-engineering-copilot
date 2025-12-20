using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.KnowledgeBase;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Services.Azure;

namespace Platform.Engineering.Copilot.KnowledgeBase.Agent.Plugins;

/// <summary>
/// Kernel plugin for RMF/STIG/DoD knowledge base queries and explanations.
/// Provides compliant advisory-only information about compliance frameworks.
/// </summary>
public class KnowledgeBasePlugin : BaseSupervisorPlugin
{
    private readonly IRmfKnowledgeService _rmfService;
    private readonly IStigKnowledgeService _stigService;
    private readonly IDoDInstructionService _dodInstructionService;
    private readonly IDoDWorkflowService _workflowService;
    private readonly AzureMcpClient _azureMcpClient;
    private readonly INistControlsService _nistControlsService;
    private readonly IImpactLevelService _impactLevelService;
    private readonly IFedRampTemplateService _fedRampTemplateService;
    

    public KnowledgeBasePlugin(
        IRmfKnowledgeService rmfService,
        IStigKnowledgeService stigService,
        IDoDInstructionService dodInstructionService,
        IDoDWorkflowService workflowService,
        AzureMcpClient azureMcpClient,
        INistControlsService nistControlsService,
        IImpactLevelService impactLevelService,
        IFedRampTemplateService fedRampTemplateService,
        ILogger<KnowledgeBasePlugin> logger,
        Kernel kernel) : base(logger, kernel)
    {
        _rmfService = rmfService ?? throw new ArgumentNullException(nameof(rmfService));
        _stigService = stigService ?? throw new ArgumentNullException(nameof(stigService));
        _dodInstructionService = dodInstructionService ?? throw new ArgumentNullException(nameof(dodInstructionService));
        _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
        _azureMcpClient = azureMcpClient ?? throw new ArgumentNullException(nameof(azureMcpClient));
        _nistControlsService = nistControlsService ?? throw new ArgumentNullException(nameof(nistControlsService));
        _impactLevelService = impactLevelService ?? throw new ArgumentNullException(nameof(impactLevelService));
        _fedRampTemplateService = fedRampTemplateService ?? throw new ArgumentNullException(nameof(fedRampTemplateService));
    }

    #region NIST Control Explanations

    [KernelFunction("explain_nist_control")]
    [Description("📚 KNOWLEDGE/DEFINITION ONLY - USE THIS for questions about what a NIST 800-53 control IS or MEANS. " +
                 "Returns the control definition, requirements, and Azure implementation guidance WITHOUT scanning. " +
                 "✅ USE THIS when user asks: 'What is AC-2?', 'Explain SC-28', 'Define IA-2', 'What does CM-6 require?', 'Tell me about control X', 'Describe control Y'. " +
                 "✅ KEYWORDS that indicate this function: 'what is', 'explain', 'define', 'describe', 'tell me about', 'what does X mean', 'what does X require'. " +
                 "🚫 DO NOT use run_compliance_assessment for these questions - that would scan the environment unnecessarily. " +
                 "This is purely educational/informational - no environment scanning occurs.")]
    public async Task<string> ExplainNistControlAsync(
        [Description("NIST 800-53 control ID (e.g., 'AC-2', 'IA-2', 'SC-28', 'CM-6'). Include enhancement number if asking about a specific enhancement (e.g., 'IA-2(1)', 'AC-2(4)').")]
        string controlId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Explaining NIST 800-53 control {ControlId}", controlId);
        
        try
        {
            // Normalize the control ID (uppercase, trim whitespace)
            var normalizedId = controlId?.Trim().ToUpperInvariant() ?? "";
            
            // Get the control from the NIST catalog
            var control = await _nistControlsService.GetControlAsync(normalizedId, cancellationToken);
            
            if (control == null)
            {
                // Try without the enhancement part if it's an enhancement
                if (normalizedId.Contains('('))
                {
                    var baseControlId = normalizedId.Split('(')[0];
                    control = await _nistControlsService.GetControlAsync(baseControlId, cancellationToken);
                    
                    if (control != null)
                    {
                        return $@"# 📚 NIST 800-53 Control: {normalizedId}

**Note:** This is an enhancement of base control {baseControlId}.

## Base Control: {control.Title}

{GetControlStatement(control)}

{GetControlGuidance(control)}

{GetAzureImplementationGuidance(normalizedId)}

---
*This is informational only. To check your compliance status, ask: 'Run a compliance assessment'*";
                    }
                }
                
                return $@"❓ Control '{controlId}' was not found in the NIST 800-53 catalog.

**Suggestions:**
- Check the control ID format (e.g., AC-2, IA-2(1), SC-28)
- Use 'search for NIST controls about [topic]' to find related controls
- Common control families: AC (Access Control), AU (Audit), IA (Identification), SC (System Protection), CM (Configuration)
";
            }
            
            // Build the explanation response
            var response = $@"# 📚 NIST 800-53 Control: {control.Id}

## {control.Title}

{GetControlStatement(control)}

{GetControlGuidance(control)}

{GetAzureImplementationGuidance(control.Id ?? "")}

{GetRelatedControls(control)}

---
*This is informational only. To check your compliance status, ask: 'Run a compliance assessment'*";
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining NIST control {ControlId}", controlId);
            return $"Error retrieving information for control {controlId}. Please try again or check the control ID format.";
        }
    }
    
    private string GetControlStatement(Core.Models.Compliance.NistControl control)
    {
        var statement = control.Parts?
            .FirstOrDefault(p => p.Name?.Equals("statement", StringComparison.OrdinalIgnoreCase) == true);
        
        if (statement?.Prose != null)
        {
            return $"### Control Statement\n\n{statement.Prose}";
        }
        
        // Try to get nested statement parts
        if (statement?.Parts != null && statement.Parts.Any())
        {
            var parts = statement.Parts.Select(p => $"- {p.Prose}").ToList();
            return $"### Control Statement\n\n{string.Join("\n", parts)}";
        }
        
        return "### Control Statement\n\n*Statement not available in catalog.*";
    }
    
    private string GetControlGuidance(Core.Models.Compliance.NistControl control)
    {
        var guidance = control.Parts?
            .FirstOrDefault(p => p.Name?.Equals("guidance", StringComparison.OrdinalIgnoreCase) == true);
        
        if (guidance?.Prose != null)
        {
            return $"### Supplemental Guidance\n\n{guidance.Prose}";
        }
        
        return "";
    }
    
    private string GetAzureImplementationGuidance(string controlId)
    {
        // Provide Azure-specific implementation guidance for common controls
        var azureGuidance = controlId.ToUpperInvariant() switch
        {
            "AC-2" => @"### 🔵 Azure Implementation

- **Azure AD**: Use Azure Active Directory for centralized identity management
- **RBAC**: Implement Role-Based Access Control with least privilege principle
- **PIM**: Use Privileged Identity Management for just-in-time access
- **Access Reviews**: Configure periodic access reviews in Azure AD
- **Conditional Access**: Enforce MFA and device compliance policies",
            
            "AC-3" => @"### 🔵 Azure Implementation

- **RBAC**: Use built-in and custom roles to enforce access policies
- **Azure Policy**: Define and enforce organizational access standards
- **Resource Locks**: Prevent accidental deletion or modification
- **Management Groups**: Organize subscriptions with hierarchical access control",
            
            "AC-6" => @"### 🔵 Azure Implementation

- **RBAC**: Assign minimum necessary permissions
- **PIM**: Use time-bound, approval-required access for privileged roles
- **Custom Roles**: Create roles with only required permissions
- **Deny Assignments**: Explicitly block specific actions when needed",
            
            "IA-2" => @"### 🔵 Azure Implementation

- **Azure AD**: Centralized authentication for all users
- **MFA**: Require multi-factor authentication
- **Conditional Access**: Risk-based authentication policies
- **Password Protection**: Azure AD Password Protection
- **FIDO2/Passwordless**: Support for modern authentication methods",
            
            "SC-7" => @"### 🔵 Azure Implementation

- **Azure Firewall**: Centralized network security
- **NSGs**: Network Security Groups for subnet/NIC filtering
- **Private Endpoints**: Keep traffic on Microsoft backbone
- **VNet Peering**: Controlled connectivity between networks
- **Azure DDoS Protection**: Protect against volumetric attacks",
            
            "SC-28" => @"### 🔵 Azure Implementation

- **Azure Disk Encryption**: Encrypt VM disks with BitLocker/DM-Crypt
- **Storage Service Encryption**: Automatic encryption for Azure Storage
- **Azure SQL TDE**: Transparent Data Encryption for databases
- **Key Vault**: Centralized key management with HSM backing
- **Customer-Managed Keys**: Use your own encryption keys",
            
            "AU-2" => @"### 🔵 Azure Implementation

- **Azure Monitor**: Centralized logging and monitoring
- **Diagnostic Settings**: Route logs to Log Analytics, Storage, Event Hub
- **Activity Logs**: Track control plane operations
- **Microsoft Defender for Cloud**: Security event monitoring
- **Azure Sentinel**: SIEM for advanced threat detection",
            
            "CM-6" => @"### 🔵 Azure Implementation

- **Azure Policy**: Enforce configuration standards
- **Azure Blueprints**: Deploy compliant environments
- **ARM Templates/Bicep**: Infrastructure as Code for consistency
- **Azure Automation**: Configuration management and DSC
- **Update Management**: Automated patching",
            
            _ => @"### 🔵 Azure Implementation

*For specific Azure implementation guidance for this control, consider:*
- Review Microsoft Compliance documentation
- Check Azure Security Benchmark mappings
- Use Microsoft Defender for Cloud recommendations
- Consult Azure Well-Architected Framework security pillar"
        };
        
        return azureGuidance;
    }
    
    private string GetRelatedControls(Core.Models.Compliance.NistControl control)
    {
        // Check for sub-controls/enhancements
        if (control.Controls != null && control.Controls.Any())
        {
            var enhancements = control.Controls.Take(5).Select(c => $"- **{c.Id}**: {c.Title}");
            return $"### Related Enhancements\n\n{string.Join("\n", enhancements)}\n\n*Ask about any enhancement by its ID (e.g., '{control.Id}(1)')*";
        }
        
        return "";
    }

    [KernelFunction("explain_control_with_azure_implementation")]
    [Description("🔵 COMBINED QUERY: Explains a NIST 800-53 control AND shows which Azure services implement it. " +
                 "Use this when user asks BOTH what a control is AND how Azure implements it. " +
                 "✅ USE for: 'What is IA-2 and what Azure services implement it?', " +
                 "'Explain SC-28 and show Azure implementation', " +
                 "'What is control AC-2 and how is it implemented in Azure?'. " +
                 "This provides: (1) Control definition, (2) Requirements, (3) Azure service mappings, (4) Implementation guidance. " +
                 "Does NOT perform live compliance scanning - for that, follow up with 'run a compliance scan'.")]
    public async Task<string> ExplainControlWithAzureImplementationAsync(
        [Description("NIST 800-53 control ID (e.g., 'AC-2', 'IA-2', 'SC-28')")] string controlId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Explaining NIST control {ControlId} with Azure implementation details", controlId);
        
        try
        {
            var normalizedId = controlId?.Trim().ToUpperInvariant() ?? "";
            var control = await _nistControlsService.GetControlAsync(normalizedId, cancellationToken);
            
            if (control == null)
            {
                return $@"❓ Control '{controlId}' was not found in the NIST 800-53 catalog.

**Suggestions:**
- Check the control ID format (e.g., AC-2, IA-2, SC-28)
- Common families: AC (Access), AU (Audit), IA (Identification), SC (System Protection)";
            }
            
            var azureServices = GetAzureServicesForControl(normalizedId);
            var implementationDetails = GetDetailedAzureImplementation(normalizedId);
            
            return $@"# 📚 NIST 800-53 Control: {control.Id} - {control.Title}

{GetControlStatement(control)}

{GetControlGuidance(control)}

---

## 🔵 Azure Services That Implement {control.Id}

{azureServices}

## 📋 Implementation Details

{implementationDetails}

{GetRelatedControls(control)}

---

### 🎯 Next Steps

To verify your Azure resources comply with {control.Id}:
```
Run a compliance scan on my subscription
```

Or check a specific resource group:
```
Run a compliance scan on resource group <your-rg-name>
```";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining control {ControlId} with Azure implementation", controlId);
            return $"Error retrieving information for control {controlId}. Please try again.";
        }
    }
    
    private string GetAzureServicesForControl(string controlId)
    {
        return controlId switch
        {
            "IA-2" => @"| Azure Service | Implementation |
|---------------|----------------|
| **Azure Active Directory** | Primary identity provider for all authentication |
| **Azure MFA** | Multi-factor authentication enforcement |
| **Conditional Access** | Risk-based authentication policies |
| **Azure AD B2B/B2C** | External user authentication |
| **Managed Identities** | Service-to-service authentication |
| **FIDO2 Security Keys** | Passwordless authentication |
| **Certificate-Based Auth** | CAC/PIV smart card support |",

            "AC-2" => @"| Azure Service | Implementation |
|---------------|----------------|
| **Azure Active Directory** | Centralized identity and account management |
| **Azure RBAC** | Role-based access control |
| **Privileged Identity Management (PIM)** | Just-in-time privileged access |
| **Access Reviews** | Periodic account access review |
| **Azure AD Groups** | Group-based account management |
| **Identity Governance** | Account lifecycle management |",

            "SC-28" => @"| Azure Service | Implementation |
|---------------|----------------|
| **Azure Storage Service Encryption** | Automatic encryption for blobs, files, queues |
| **Azure Disk Encryption** | VM disk encryption (BitLocker/DM-Crypt) |
| **Azure SQL TDE** | Transparent Data Encryption for databases |
| **Azure Key Vault** | Centralized key management |
| **Customer-Managed Keys (CMK)** | Bring your own encryption keys |
| **Azure Confidential Computing** | Encryption in use |",

            "AU-2" => @"| Azure Service | Implementation |
|---------------|----------------|
| **Azure Monitor** | Centralized logging platform |
| **Log Analytics** | Log aggregation and query |
| **Activity Logs** | Control plane audit events |
| **Diagnostic Settings** | Resource-level logging |
| **Microsoft Defender for Cloud** | Security event logging |
| **Azure Sentinel** | SIEM and threat detection |",

            "SC-7" => @"| Azure Service | Implementation |
|---------------|----------------|
| **Azure Firewall** | Centralized network security |
| **Network Security Groups (NSG)** | Subnet/NIC traffic filtering |
| **Azure Front Door** | Global edge security |
| **Azure DDoS Protection** | Volumetric attack protection |
| **Private Endpoints** | Private network connectivity |
| **Azure Bastion** | Secure VM access without public IPs |",

            "CM-6" => @"| Azure Service | Implementation |
|---------------|----------------|
| **Azure Policy** | Configuration enforcement |
| **Azure Blueprints** | Environment templates |
| **ARM Templates/Bicep** | Infrastructure as Code |
| **Azure Automation DSC** | Desired State Configuration |
| **Azure Arc** | Hybrid configuration management |
| **Update Management** | Patch management |",

            _ => @"| Category | Azure Services |
|----------|----------------|
| **Identity** | Azure AD, MFA, PIM, Conditional Access |
| **Network** | NSG, Azure Firewall, Private Endpoints |
| **Data** | Storage Encryption, TDE, Key Vault |
| **Monitoring** | Azure Monitor, Log Analytics, Defender |
| **Configuration** | Azure Policy, Blueprints, ARM/Bicep |

*For specific service mappings, check Microsoft Compliance documentation.*"
        };
    }
    
    private string GetDetailedAzureImplementation(string controlId)
    {
        return controlId switch
        {
            "IA-2" => @"### Required Configurations

1. **Enable Azure MFA** for all users (at minimum, privileged users)
2. **Configure Conditional Access** policies requiring MFA for:
   - Azure portal access
   - Admin actions
   - Sensitive applications
3. **Enable Sign-in Risk Policies** in Azure AD Identity Protection
4. **Configure Password Protection** to ban common passwords
5. **Consider Passwordless** options (FIDO2, Windows Hello, Authenticator)

### Azure CLI Quick Check
```bash
# Check MFA registration status
az ad user list --query ""[].{Name:displayName,MFA:strongAuthenticationMethods}"" -o table

# List Conditional Access policies
az rest --method GET --uri ""https://graph.microsoft.com/v1.0/identity/conditionalAccess/policies""
```",

            "AC-2" => @"### Required Configurations

1. **Implement RBAC** with least privilege principle
2. **Enable PIM** for privileged role assignments
3. **Configure Access Reviews** for periodic validation
4. **Set up Account Lifecycle** policies (onboarding/offboarding)
5. **Enable Sign-in Logs** for account activity monitoring

### Azure CLI Quick Check
```bash
# List role assignments
az role assignment list --all --query ""[].{Principal:principalName,Role:roleDefinitionName}"" -o table

# Check PIM settings
az rest --method GET --uri ""https://graph.microsoft.com/v1.0/roleManagement/directory/roleAssignments""
```",

            "SC-28" => @"### Required Configurations

1. **Enable Storage Service Encryption** (default, verify it's not disabled)
2. **Enable Azure Disk Encryption** for all VMs
3. **Enable TDE** for all Azure SQL databases
4. **Use Key Vault** for key management
5. **Consider Customer-Managed Keys** for sensitive workloads

### Azure CLI Quick Check
```bash
# Check storage encryption
az storage account list --query ""[].{Name:name,Encryption:encryption.services.blob.enabled}"" -o table

# Check VM disk encryption
az vm encryption show --name <vm-name> --resource-group <rg-name>
```",

            _ => @"### General Implementation Steps

1. Review Microsoft documentation for this specific control
2. Check Azure Security Benchmark mappings
3. Use Microsoft Defender for Cloud recommendations
4. Implement Azure Policy for continuous compliance
5. Enable appropriate logging and monitoring

### Resources
- [Azure Security Benchmark](https://docs.microsoft.com/azure/security/benchmarks/)
- [Microsoft Compliance Manager](https://compliance.microsoft.com)
- [Defender for Cloud Recommendations](https://docs.microsoft.com/azure/defender-for-cloud/)"
        };
    }

    #endregion

    #region RMF Functions

    [KernelFunction("explain_rmf_process")]
    [Description("Explain the Risk Management Framework (RMF) process for DoD systems. " +
                 "Provides overview of all 6 RMF steps or detailed explanation of a specific step. " +
                 "Use this when users ask about RMF, ATO process, or authorization workflows. " +
                 "Examples: 'Explain RMF', 'What is RMF Step 4?', 'How do I get an ATO?'")]
    public async Task<string> ExplainRmfProcessAsync(
        [Description("Optional: Specific RMF step number (1-6). Leave empty for complete overview.")] string? step = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Explaining RMF process{Step}", step != null ? $" step {step}" : "");
        
        try
        {
            return await _rmfService.ExplainRmfProcessAsync(step, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining RMF process");
            return "Error retrieving RMF process information. Please check logs for details.";
        }
    }

    [KernelFunction("get_rmf_deliverables")]
    [Description("Get required deliverables for a specific RMF step. " +
                 "Returns list of documents and artifacts needed for each RMF phase. " +
                 "Use when users ask 'What documents do I need for RMF Step X?'")]
    public async Task<string> GetRmfDeliverablesAsync(
        [Description("RMF step number (1-6)")] string step,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting RMF deliverables for step {Step}", step);
        
        try
        {
            var deliverables = await _rmfService.GetRmfOutputsForStepAsync(step, cancellationToken);
            
            if (!deliverables.Any())
                return $"No deliverables found for RMF Step {step}. Valid steps are 1-6.";

            return $@"**RMF Step {step} Required Deliverables:**

{string.Join("\n", deliverables.Select((d, i) => $"{i + 1}. {d}"))}

Use the 'explain_rmf_process' function with step={step} for complete details about this phase.
";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting RMF deliverables");
            return "Error retrieving RMF deliverables. Please check logs for details.";
        }
    }

    #endregion

    #region STIG Functions

    [KernelFunction("explain_stig")]
    [Description("Explain a specific STIG (Security Technical Implementation Guide) control. " +
                 "Provides detailed information including severity, check procedures, remediation steps, and Azure implementation. " +
                 "Use when users ask about specific STIG IDs like 'Explain STIG V-219153' or 'How do I fix V-219187?'")]
    public async Task<string> ExplainStigAsync(
        [Description("STIG ID (e.g., 'V-219153') or Vulnerability ID")] string stigId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Explaining STIG {StigId}", stigId);
        
        try
        {
            return await _stigService.ExplainStigAsync(stigId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining STIG");
            return $"Error retrieving STIG {stigId} information. Please check logs for details.";
        }
    }

    [KernelFunction("search_stigs")]
    [Description("Search for STIG controls by keyword or topic. " +
                 "Use when users ask general questions like 'What STIGs apply to encryption?' or 'STIGs for MFA'")]
    public async Task<string> SearchStigsAsync(
        [Description("Search term or keyword (e.g., 'encryption', 'MFA', 'public IP')")] string searchTerm,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching STIGs for: {SearchTerm}", searchTerm);
        
        try
        {
            var results = await _stigService.SearchStigsAsync(searchTerm, cancellationToken);
            
            if (!results.Any())
                return $"No STIG controls found matching '{searchTerm}'. Try broader search terms.";

            return $@"**STIG Search Results for ""{searchTerm}""**

Found {results.Count} matching controls:

{string.Join("\n\n", results.Take(5).Select(s => $@"**{s.StigId}**: {s.Title}
- Severity: {s.Severity}
- Category: {s.Category}
- NIST Controls: {string.Join(", ", s.NistControls)}
- Description: {(s.Description.Length > 150 ? s.Description.Substring(0, 150) + "..." : s.Description)}"))}

{(results.Count > 5 ? $"\n...and {results.Count - 5} more results.\n" : "")}

Use 'explain_stig' function with a specific STIG ID for complete details.
";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching STIGs");
            return "Error searching STIG controls. Please check logs for details.";
        }
    }

    [KernelFunction("get_stigs_for_nist_control")]
    [Description("Get all STIG controls that map to a specific NIST 800-53 control. " +
                 "Use when users ask 'What STIGs implement AC-2?' or 'Show STIGs for control IA-2(1)'")]
    public async Task<string> GetStigsForNistControlAsync(
        [Description("NIST 800-53 control ID (e.g., 'AC-2', 'IA-2(1)', 'SC-28')")] string nistControlId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting STIGs for NIST control {ControlId}", nistControlId);
        
        try
        {
            var stigs = await _stigService.GetStigsByNistControlAsync(nistControlId, cancellationToken);
            
            if (!stigs.Any())
                return $"No STIG controls found for NIST control {nistControlId}.";

            return $@"**STIG Controls for NIST {nistControlId}**

Found {stigs.Count} STIG control{(stigs.Count > 1 ? "s" : "")}:

{string.Join("\n", stigs.Select(s => $"- **{s.StigId}**: {s.Title} (Severity: {s.Severity})"))}

Use 'explain_stig' for detailed information on any of these controls.
";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting STIGs for NIST control");
            return "Error retrieving STIG mappings. Please check logs for details.";
        }
    }

    [KernelFunction("get_control_mapping")]
    [Description("Get complete mapping between NIST 800-53 controls, STIGs, CCIs, and DoD instructions. " +
                 "Use when users ask about compliance mappings or relationships between frameworks.")]
    public async Task<string> GetControlMappingAsync(
        [Description("NIST 800-53 control ID (e.g., 'AC-2', 'IA-2(1)')")] string nistControlId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting control mapping for {ControlId}", nistControlId);
        
        try
        {
            var mapping = await _stigService.GetControlMappingAsync(nistControlId, cancellationToken);
            
            if (mapping == null)
                return $"No control mapping found for NIST control {nistControlId}.";

            return $@"**Control Mapping for NIST {nistControlId}**

{mapping.Description}

**STIG IDs:** {string.Join(", ", mapping.StigIds)}
**CCI References:** {string.Join(", ", mapping.CciIds)}
**DoD Instructions:** {string.Join(", ", mapping.DoDInstructions)}

**Implementation Guidance:**
{string.Join("\n", mapping.ImplementationGuidance.Select(kvp => $"- **{kvp.Key}**: {kvp.Value}"))}
";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting control mapping");
            return "Error retrieving control mapping. Please check logs for details.";
        }
    }

    #endregion

    #region DoD Instruction Functions

    [KernelFunction("explain_dod_instruction")]
    [Description("Explain a DoD Instruction and its requirements. " +
                 "Use when users ask about specific DoD policies like 'Explain DoDI 8500.01' or 'What is DoDI 8510.01?'")]
    public async Task<string> ExplainDoDInstructionAsync(
        [Description("DoD Instruction ID (e.g., 'DoDI 8500.01', 'CNSSI 1253')")] string instructionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Explaining DoD Instruction {InstructionId}", instructionId);
        
        try
        {
            return await _dodInstructionService.ExplainInstructionAsync(instructionId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining DoD instruction");
            return $"Error retrieving DoD Instruction {instructionId}. Please check logs for details.";
        }
    }

    [KernelFunction("search_dod_instructions")]
    [Description("Search for DoD Instructions by keyword or topic. " +
                 "Use when users ask about DoD policies on specific topics like 'DoD cybersecurity policy' or 'PKI instructions'")]
    public async Task<string> SearchDoDInstructionsAsync(
        [Description("Search term or keyword")] string searchTerm,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching DoD instructions for: {SearchTerm}", searchTerm);
        
        try
        {
            var results = await _dodInstructionService.SearchInstructionsAsync(searchTerm, cancellationToken);
            
            if (!results.Any())
                return $"No DoD Instructions found matching '{searchTerm}'.";

            return $@"**DoD Instruction Search Results**

Found {results.Count} instruction{(results.Count > 1 ? "s" : "")}:

{string.Join("\n\n", results.Select(i => $@"**{i.InstructionId}**: {i.Title}
{i.Description}
Applicability: {i.Applicability}"))}

Use 'explain_dod_instruction' for complete details.
";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching DoD instructions");
            return "Error searching DoD instructions. Please check logs for details.";
        }
    }

    [KernelFunction("get_control_with_dod_instructions")]
    [Description("Get NIST 800-53 control details with related DoD instruction references. " +
                 "Shows which DoD instructions mandate this control with specific sections and requirements. " +
                 "Use when users ask 'What DoD instructions require AC-2?' or 'Show DoD guidance for SC-7'")]
    public async Task<string> GetControlWithDoDInstructionsAsync(
        [Description("NIST 800-53 control ID (e.g., 'AC-2', 'SC-7', 'IA-2(1)')")] string nistControlId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting DoD instructions for NIST control {ControlId}", nistControlId);
        
        try
        {
            var dodInstructions = await _dodInstructionService.GetInstructionsByControlAsync(
                nistControlId, 
                cancellationToken);
            
            if (!dodInstructions.Any())
                return $"No DoD instructions found for NIST control {nistControlId}.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# DoD Instructions for NIST {nistControlId}");
            sb.AppendLine();
            sb.AppendLine($"Found {dodInstructions.Count} DoD instruction{(dodInstructions.Count > 1 ? "s" : "")} with requirements for this control:");
            sb.AppendLine();
            
            foreach (var instruction in dodInstructions)
            {
                var mappings = instruction.ControlMappings
                    .Where(m => m.NistControlId.Equals(nistControlId, StringComparison.OrdinalIgnoreCase));
                
                foreach (var mapping in mappings)
                {
                    sb.AppendLine($"## {instruction.InstructionId}: {instruction.Title}");
                    sb.AppendLine();
                    sb.AppendLine($"**Section:** {mapping.Section}");
                    sb.AppendLine($"**Requirement:** {mapping.Requirement}");
                    sb.AppendLine($"**Applicable Impact Levels:** {mapping.ImpactLevel}");
                    sb.AppendLine();
                    sb.AppendLine($"**Full Instruction:** {instruction.Url}");
                    sb.AppendLine();
                    sb.AppendLine("---");
                    sb.AppendLine();
                }
            }
            
            sb.AppendLine("Use `explain_dod_instruction` for complete details on any instruction.");
            sb.AppendLine("Use `explain_impact_level` to understand Impact Level requirements.");
            
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting DoD instructions for control");
            return "Error retrieving DoD instruction mappings. Please check logs for details.";
        }
    }

    #endregion

    #region Workflow Functions

    [KernelFunction("explain_navy_workflow")]
    [Description("Explain a Navy/DoD workflow process (e.g., ATO process, eMASS registration, PMW deployment). " +
                 "Use when users ask 'How do I get an ATO in Navy?' or 'What is the PMW cloud deployment process?'")]
    public async Task<string> ExplainNavyWorkflowAsync(
        [Description("Workflow ID (e.g., 'WF-NAV-ATO-001', 'WF-PMW-CLOUD-001', 'WF-NAV-EMASS-001')")] string workflowId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Explaining Navy workflow {WorkflowId}", workflowId);
        
        try
        {
            return await _workflowService.ExplainWorkflowAsync(workflowId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining Navy workflow");
            return $"Error retrieving workflow {workflowId}. Please check logs for details.";
        }
    }

    [KernelFunction("get_navy_ato_process")]
    [Description("Get the complete Navy RMF/ATO authorization process with all steps and timelines. " +
                 "Use when users ask 'How do I get an ATO?' or 'What is the Navy authorization process?'")]
    public async Task<string> GetNavyAtoProcessAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting Navy ATO process");
        return await ExplainNavyWorkflowAsync("WF-NAV-ATO-001", cancellationToken);
    }

    [KernelFunction("get_pmw_deployment_process")]
    [Description("Get the PMW (Program Manager Warfare Systems) cloud deployment workflow. " +
                 "Use for PMW-specific cloud system deployment guidance.")]
    public async Task<string> GetPmwDeploymentProcessAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting PMW deployment process");
        return await ExplainNavyWorkflowAsync("WF-PMW-CLOUD-001", cancellationToken);
    }

    [KernelFunction("get_emass_registration_process")]
    [Description("Get the eMASS (Enterprise Mission Assurance Support Service) system registration process. " +
                 "Use when users ask about registering systems in eMASS or maintaining eMASS records.")]
    public async Task<string> GetEmassRegistrationProcessAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting eMASS registration process");
        return await ExplainNavyWorkflowAsync("WF-NAV-EMASS-001", cancellationToken);
    }

    #endregion

    #region IL-Specific Guidance

    [KernelFunction("explain_impact_level")]
    [Description("Explain Impact Level (IL) requirements for DoD cloud systems. " +
                 "Covers IL2, IL4, IL5, and IL6 with specific Azure configurations, controls, and restrictions. " +
                 "Use when users ask 'What is IL5?' or 'IL6 boundary protection requirements?'")]
    public async Task<string> ExplainImpactLevelAsync(
        [Description("Impact Level (IL2, IL4, IL5, or IL6)")] string impactLevel,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Explaining Impact Level {Level}", impactLevel);
        
        try
        {
            return await _impactLevelService.ExplainImpactLevelAsync(impactLevel, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining impact level {Level}", impactLevel);
            return $"Error retrieving Impact Level information for {impactLevel}.";
        }
    }

    [KernelFunction("compare_impact_levels")]
    [Description("Compare all DoD Impact Levels (IL2, IL4, IL5, IL6) side by side. " +
                 "Shows differences in data types, encryption, network, personnel requirements. " +
                 "Use when users ask 'Compare IL4 and IL5' or 'What are the differences between impact levels?'")]
    public async Task<string> CompareImpactLevelsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting Impact Level comparison");
        
        try
        {
            return await _impactLevelService.GetImpactLevelComparisonAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting impact level comparison");
            return "Error retrieving Impact Level comparison.";
        }
    }

    [KernelFunction("get_migration_guidance")]
    [Description("Get guidance for migrating between DoD Impact Levels. " +
                 "Covers IL2 to IL4, IL4 to IL5 migration steps and requirements. " +
                 "Use when users ask 'How do I migrate from IL4 to IL5?' or 'Steps to upgrade to IL5'")]
    public async Task<string> GetMigrationGuidanceAsync(
        [Description("Source Impact Level (e.g., IL2, IL4)")] string fromLevel,
        [Description("Target Impact Level (e.g., IL4, IL5)")] string toLevel,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting migration guidance from {From} to {To}", fromLevel, toLevel);
        
        try
        {
            return await _impactLevelService.GetMigrationGuidanceAsync(fromLevel, toLevel, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting migration guidance");
            return $"Error retrieving migration guidance from {fromLevel} to {toLevel}.";
        }
    }

    [KernelFunction("get_azure_implementation_for_il")]
    [Description("Get Azure-specific implementation guidance for a DoD Impact Level. " +
                 "Shows required regions, services, encryption, and compliance checklist. " +
                 "Use when users ask 'How do I configure Azure for IL5?' or 'Azure requirements for IL4'")]
    public async Task<string> GetAzureImplementationForILAsync(
        [Description("Impact Level (IL2, IL4, IL5, or IL6)")] string impactLevel,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting Azure implementation for {Level}", impactLevel);
        
        try
        {
            return await _impactLevelService.GetAzureImplementationAsync(impactLevel, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Azure implementation for {Level}", impactLevel);
            return $"Error retrieving Azure implementation for {impactLevel}.";
        }
    }

    #endregion

    #region FedRAMP Templates

    [KernelFunction("get_ssp_section_template")]
    [Description("Get a System Security Plan (SSP) section template with Azure mapping. " +
                 "Returns required elements, example content, and Azure data sources for the section. " +
                 "Use when users ask 'SSP section 5 template' or 'What goes in SSP system environment section?'")]
    public async Task<string> GetSspSectionTemplateAsync(
        [Description("SSP section number (1-9)")] string sectionNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting SSP section template for section {Section}", sectionNumber);
        
        try
        {
            return await _fedRampTemplateService.GetSspSectionTemplateAsync(sectionNumber, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting SSP section template");
            return $"Error retrieving SSP section {sectionNumber} template.";
        }
    }

    [KernelFunction("get_control_narrative_template")]
    [Description("Get an Azure-specific SSP control narrative template. " +
                 "Returns a ready-to-use narrative with Azure service references. " +
                 "Use when users ask 'Give me AC-2 narrative for SSP' or 'Control narrative template for SC-7'")]
    public async Task<string> GetControlNarrativeTemplateAsync(
        [Description("NIST 800-53 control ID (e.g., AC-2, SC-7, IA-2)")] string controlId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting control narrative template for {ControlId}", controlId);
        
        try
        {
            return await _fedRampTemplateService.GetControlNarrativeAsync(controlId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting control narrative template");
            return $"Error retrieving control narrative for {controlId}.";
        }
    }

    [KernelFunction("get_poam_template")]
    [Description("Get POA&M (Plan of Action and Milestones) template with Azure integration guidance. " +
                 "Shows required fields, examples, and how to integrate with Azure DevOps/Defender for Cloud. " +
                 "Use when users ask 'POA&M template' or 'How do I track findings?'")]
    public async Task<string> GetPoamTemplateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting POA&M template");
        
        try
        {
            return await _fedRampTemplateService.GetPoamTemplateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting POA&M template");
            return "Error retrieving POA&M template.";
        }
    }

    [KernelFunction("get_continuous_monitoring_requirements")]
    [Description("Get FedRAMP continuous monitoring requirements with Azure implementation. " +
                 "Shows required activities, frequencies, and Azure services to use. " +
                 "Use when users ask 'What is required for continuous monitoring?' or 'ConMon requirements'")]
    public async Task<string> GetContinuousMonitoringRequirementsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting continuous monitoring requirements");
        
        try
        {
            return await _fedRampTemplateService.GetContinuousMonitoringRequirementsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting continuous monitoring requirements");
            return "Error retrieving continuous monitoring requirements.";
        }
    }

    [KernelFunction("get_authorization_package_checklist")]
    [Description("Get FedRAMP/DoD authorization package document checklist. " +
                 "Lists all required and optional documents for ATO package. " +
                 "Use when users ask 'What documents do I need for ATO?' or 'Authorization package checklist'")]
    public async Task<string> GetAuthorizationPackageChecklistAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting authorization package checklist");
        
        try
        {
            var checklist = await _fedRampTemplateService.GetAuthorizationPackageChecklistAsync(cancellationToken);
            return "# Authorization Package Checklist\n\n" + string.Join("\n", checklist.Select(c => $"- {c}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authorization package checklist");
            return "Error retrieving authorization package checklist.";
        }
    }

    [KernelFunction("explain_authorization_path")]
    [Description("Explain a FedRAMP authorization path (Agency, JAB, or DoD PA). " +
                 "Shows timeline, requirements, and process for the authorization type. " +
                 "Use when users ask 'How does JAB authorization work?' or 'Agency vs JAB authorization'")]
    public async Task<string> ExplainAuthorizationPathAsync(
        [Description("Authorization path (Agency, JAB, or DoD PA)")] string path,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Explaining authorization path {Path}", path);
        
        try
        {
            return await _fedRampTemplateService.ExplainAuthorizationPathAsync(path, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining authorization path");
            return $"Error retrieving authorization path information for {path}.";
        }
    }

    #endregion

    #region Enhanced RMF

    [KernelFunction("get_service_rmf_guidance")]
    [Description("Get service-specific RMF guidance for Navy, Army, Air Force, or DISA. " +
                 "Shows authorizing organization, contacts, tools, and workflows. " +
                 "Use when users ask 'Navy RMF process' or 'How does Army do ATO?'")]
    public async Task<string> GetServiceRmfGuidanceAsync(
        [Description("Military service (Navy, Army, Air Force, or DISA)")] string service,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting RMF guidance for {Service}", service);
        
        try
        {
            return await _rmfService.GetServiceSpecificGuidanceAsync(service, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting service RMF guidance");
            return $"Error retrieving RMF guidance for {service}.";
        }
    }

    [KernelFunction("get_rmf_timeline")]
    [Description("Get typical RMF authorization timelines for different scenarios. " +
                 "Shows phases and durations for new systems, cloud migrations, or reciprocity. " +
                 "Use when users ask 'How long does ATO take?' or 'RMF timeline for cloud'")]
    public async Task<string> GetRmfTimelineAsync(
        [Description("Authorization type (new system, cloud migration, or reciprocity)")] string authorizationType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting RMF timeline for {Type}", authorizationType);
        
        try
        {
            return await _rmfService.GetRmfTimelineAsync(authorizationType, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting RMF timeline");
            return $"Error retrieving RMF timeline for {authorizationType}.";
        }
    }

    [KernelFunction("get_rmf_artifacts")]
    [Description("Get list of required and supporting RMF artifacts. " +
                 "Shows documents needed for authorization package. " +
                 "Use when users ask 'What artifacts do I need for RMF?' or 'List RMF documents'")]
    public async Task<string> GetRmfArtifactsAsync(
        [Description("Only show required artifacts (true/false)")] bool requiredOnly = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting RMF artifacts (required only: {Required})", requiredOnly);
        
        try
        {
            var artifacts = await _rmfService.GetRmfArtifactsAsync(requiredOnly, cancellationToken);
            return "# RMF Artifacts\n\n" + string.Join("\n", artifacts.Select(a => $"- {a}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting RMF artifacts");
            return "Error retrieving RMF artifacts.";
        }
    }

    #endregion

    #region Windows Server STIGs

    [KernelFunction("get_windows_stig_guidance")]
    [Description("Get Windows Server STIG guidance with Azure Guest Configuration mapping. " +
                 "Shows registry/GPO settings and Azure Policy enforcement options. " +
                 "Use when users ask 'Windows Server STIG V-254239' or 'How to implement Windows STIG in Azure?'")]
    public async Task<string> GetWindowsStigGuidanceAsync(
        [Description("Windows Server STIG ID (e.g., V-254239)")] string stigId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting Windows Server STIG guidance for {StigId}", stigId);
        
        try
        {
            return await _stigService.GetWindowsServerStigGuidanceAsync(stigId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Windows Server STIG guidance");
            return $"Error retrieving Windows Server STIG guidance for {stigId}.";
        }
    }

    [KernelFunction("search_windows_stigs")]
    [Description("Search Windows Server STIGs by keyword. " +
                 "Finds STIGs related to passwords, audit, RDP, SMB, etc. " +
                 "Use when users ask 'Find Windows STIGs about passwords' or 'Search Windows STIG audit'")]
    public async Task<string> SearchWindowsStigsAsync(
        [Description("Search term (e.g., password, audit, RDP, SMB)")] string searchTerm,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching Windows Server STIGs for {Term}", searchTerm);
        
        try
        {
            var results = await _stigService.SearchWindowsStigsAsync(searchTerm, cancellationToken);
            if (!results.Any())
                return $"No Windows Server STIGs found matching '{searchTerm}'.";
            
            return $"# Windows Server STIGs matching '{searchTerm}'\n\n" + 
                   string.Join("\n", results.Select(r => $"- {r}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Windows Server STIGs");
            return $"Error searching Windows Server STIGs for '{searchTerm}'.";
        }
    }

    [KernelFunction("get_guest_configuration_policy")]
    [Description("Get Azure Guest Configuration policy guidance for Windows Server STIGs. " +
                 "Shows available policy initiatives and implementation steps. " +
                 "Use when users ask 'How to enforce Windows STIGs in Azure?' or 'Guest Configuration for STIGs'")]
    public async Task<string> GetGuestConfigurationPolicyAsync(
        [Description("STIG category (optional, for filtering)")] string? stigCategory = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting Guest Configuration policy guidance");
        
        try
        {
            return await _stigService.GetGuestConfigurationPolicyAsync(stigCategory ?? "", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Guest Configuration policy guidance");
            return "Error retrieving Guest Configuration policy guidance.";
        }
    }

    #endregion

    #region STIG Cross-Reference

    [KernelFunction("get_stig_cross_reference")]
    [Description("Get comprehensive STIG cross-reference with NIST controls, CCIs, and DoD instructions. " +
                 "Shows complete compliance mapping for a STIG including all related frameworks and Azure implementation details. " +
                 "Use when users ask 'Show me all mappings for STIG V-XXXXX' or 'What NIST controls does this STIG satisfy?'")]
    public async Task<string> GetStigCrossReferenceAsync(
        [Description("STIG ID (e.g., V-219153) or Vuln ID to get cross-reference for")] string stigId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting STIG cross-reference for {StigId}", stigId);
        
        try
        {
            return await _stigService.GetStigCrossReferenceAsync(stigId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting STIG cross-reference for {StigId}", stigId);
            return $"Error retrieving STIG cross-reference for {stigId}. Please check logs for details.";
        }
    }

    [KernelFunction("get_azure_stigs")]
    [Description("Get all STIGs applicable to a specific Azure service. " +
                 "Filters STIG controls by Azure service type (e.g., Storage, Compute, Networking, Identity, Kubernetes). " +
                 "Use when users ask 'What STIGs apply to Azure Storage?' or 'Show me compute STIGs'")]
    public async Task<string> GetAzureStigsAsync(
        [Description("Azure service name (e.g., Azure Storage, Azure Virtual Machines, Azure Kubernetes Service, Azure AD, Azure Virtual Network)")] string azureService,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting Azure STIGs for service: {AzureService}", azureService);
        
        try
        {
            var stigs = await _stigService.GetAzureStigsAsync(azureService, cancellationToken);
            
            if (!stigs.Any())
                return $@"No STIGs found for Azure service: {azureService}.

Available services: Azure AD, Azure Virtual Machines, Azure Storage, Azure SQL Database, Azure Kubernetes Service, 
Azure Virtual Network, Azure Front Door, Azure Log Analytics, Azure Backup, Azure Key Vault, Azure App Service, 
Azure Functions, Azure Cosmos DB, Azure API Management, Azure Service Bus, Azure Container Registry, Azure Monitor, 
Azure Firewall, Network Security Group, Azure Policy, Microsoft Defender for Cloud, Azure Private Link.";

            var output = $@"# Azure STIGs for {azureService}

Found {stigs.Count} STIG control(s):

";
            foreach (var stig in stigs.OrderByDescending(s => s.Severity))
            {
                output += $@"## {stig.StigId}: {stig.Title}

**Severity:** {stig.Severity}
**Category:** {stig.Category}
**NIST Controls:** {string.Join(", ", stig.NistControls)}

{stig.Description}

**Azure Configuration:** {stig.AzureImplementation.GetValueOrDefault("configuration", "N/A")}
**Azure Policy:** {stig.AzureImplementation.GetValueOrDefault("azurePolicy", "N/A")}

---

";
            }

            return output;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Azure STIGs for service {AzureService}", azureService);
            return $"Error retrieving Azure STIGs for {azureService}. Please check logs for details.";
        }
    }

    [KernelFunction("get_compliance_summary")]
    [Description("Get comprehensive compliance summary for a NIST 800-53 control. " +
                 "Shows complete mapping: NIST control details + related STIGs + DoD instructions + implementation guidance. " +
                 "One-stop lookup for all compliance information. " +
                 "Use when users ask 'Show me everything about AC-4' or 'Complete compliance info for SC-28'")]
    public async Task<string> GetComplianceSummaryAsync(
        [Description("NIST 800-53 control ID (e.g., AC-4, SC-28, AU-2)")] string nistControlId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting compliance summary for NIST control {NistControlId}", nistControlId);
        
        try
        {
            var output = $"# Compliance Summary: {nistControlId}\n\n";

            // 1. Get DoD Instructions
            output += "## DoD Instructions\n\n";
            var dodInstructions = await _dodInstructionService.GetInstructionsByControlAsync(nistControlId, cancellationToken);
            
            if (dodInstructions.Any())
            {
                foreach (var instruction in dodInstructions)
                {
                    var mapping = instruction.ControlMappings?
                        .FirstOrDefault(m => m.NistControlId.Equals(nistControlId, StringComparison.OrdinalIgnoreCase));
                    
                    if (mapping != null)
                    {
                        output += $"### {instruction.Title}\n\n";
                        output += $"**Instruction ID:** {instruction.InstructionId}\n";
                        output += $"**Section:** {mapping.Section}\n";
                        output += $"**Requirement:** {mapping.Requirement}\n";
                        
                        if (!string.IsNullOrEmpty(mapping.ImpactLevel))
                            output += $"**Applicable Impact Levels:** {mapping.ImpactLevel}\n";
                        
                        output += $"**URL:** {instruction.Url}\n\n";
                    }
                }
            }
            else
            {
                output += "*No DoD instructions mapped to this control yet.*\n\n";
            }

            // 2. Get related STIGs
            output += "## Related STIG Controls\n\n";
            var stigs = await _stigService.GetStigsByNistControlAsync(nistControlId, cancellationToken);
            
            if (stigs.Any())
            {
                output += $"Found {stigs.Count} STIG control(s):\n\n";
                
                foreach (var stig in stigs.OrderByDescending(s => s.Severity))
                {
                    output += $"### {stig.StigId}: {stig.Title}\n\n";
                    output += $"**Severity:** {stig.Severity}\n";
                    output += $"**Category:** {stig.Category}\n";
                    output += $"**CCI:** {string.Join(", ", stig.CciRefs)}\n\n";
                    output += $"{stig.Description}\n\n";
                    
                    // Add Azure implementation if available
                    if (stig.AzureImplementation != null && stig.AzureImplementation.Any())
                    {
                        output += "**Azure Implementation:**\n";
                        output += $"- Service: {stig.AzureImplementation.GetValueOrDefault("service", "N/A")}\n";
                        output += $"- Configuration: {stig.AzureImplementation.GetValueOrDefault("configuration", "N/A")}\n";
                        
                        if (stig.AzureImplementation.TryGetValue("azurePolicy", out var policy))
                            output += $"- Azure Policy: {policy}\n";
                        
                        if (stig.AzureImplementation.TryGetValue("automation", out var automation))
                        {
                            output += "\n**Automation Command:**\n```bash\n";
                            output += automation;
                            output += "\n```\n";
                        }
                    }
                    
                    output += "\n---\n\n";
                }
            }
            else
            {
                output += "*No STIGs mapped to this control yet.*\n\n";
            }

            output += "## Quick Reference\n\n";
            output += $"**NIST Control:** {nistControlId}\n";
            output += $"**DoD Instructions:** {dodInstructions.Count} found\n";
            output += $"**STIGs:** {stigs.Count} found\n";

            return output;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance summary for {NistControlId}", nistControlId);
            return $"Error retrieving compliance summary for {nistControlId}. Please check logs for details.";
        }
    }

    #endregion

    #region Azure Documentation Search

    [KernelFunction("search_azure_documentation")]
    [Description("Search official Microsoft Azure documentation for guidance and troubleshooting. " +
                 "Powered by Azure MCP Server with access to up-to-date Microsoft Learn content. " +
                 "Use when you need official documentation, how-to guides, or troubleshooting steps for Azure services.")]
    public async Task<string> SearchAzureDocumentationAsync(
        [Description("Search query (e.g., 'how to configure storage firewall', 'troubleshoot AKS connectivity', 'AKS private cluster networking')")] string query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Searching Azure documentation for: {Query}", query);

            if (string.IsNullOrWhiteSpace(query))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Search query is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            await _azureMcpClient.InitializeAsync(cancellationToken);

            var docs = await _azureMcpClient.CallToolAsync("documentation", 
                new Dictionary<string, object?>
                {
                    ["query"] = query
                }, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = docs.Success,
                query = query,
                results = docs.Success ? docs.Result : "Documentation search unavailable",
                nextSteps = new[]
                {
                    "Review the documentation results above for official Microsoft guidance.",
                    "Visit https://learn.microsoft.com for the complete Azure documentation library.",
                    "For compliance-specific questions, ask about NIST controls or STIGs."
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Azure documentation for query: {Query}", query);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Error searching Azure documentation: {ex.Message}",
                query = query
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [KernelFunction("get_resource_best_practices")]
    [Description("Get Azure and Terraform best practices for specific resource types. " +
                 "Powered by Azure MCP Server with curated recommendations from Microsoft. " +
                 "Use for optimization, compliance, security, and infrastructure improvements.")]
    public async Task<string> GetResourceBestPracticesAsync(
        [Description("Resource type (e.g., 'Microsoft.Storage/storageAccounts', 'AKS', 'App Service')")] string resourceType,
        [Description("Include Terraform best practices (default: false)")] bool includeTerraform = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting best practices for resource type: {ResourceType}", resourceType);

            if (string.IsNullOrWhiteSpace(resourceType))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Resource type is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            await _azureMcpClient.InitializeAsync(cancellationToken);

            var bestPracticesResults = new List<object>();

            // 1. Get Azure best practices
            var azureBp = await _azureMcpClient.CallToolAsync("get_bestpractices", 
                new Dictionary<string, object?>
                {
                    ["resourceType"] = resourceType
                }, cancellationToken);

            bestPracticesResults.Add(new
            {
                source = "Azure",
                available = azureBp.Success,
                data = azureBp.Success ? azureBp.Result : "Azure best practices not available"
            });

            // 2. Get Terraform best practices if requested
            if (includeTerraform)
            {
                var tfBp = await _azureMcpClient.CallToolAsync("azureterraformbestpractices", 
                    new Dictionary<string, object?>
                    {
                        ["resourceType"] = resourceType
                    }, cancellationToken);

                bestPracticesResults.Add(new
                {
                    source = "Terraform",
                    available = tfBp.Success,
                    data = tfBp.Success ? tfBp.Result : "Terraform best practices not available"
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                resourceType = resourceType,
                bestPractices = bestPracticesResults,
                nextSteps = new[]
                {
                    "Review the best practices above to optimize your resources.",
                    "For compliance mapping, ask about relevant NIST controls or STIGs.",
                    "For implementation guidance, search Azure documentation for detailed steps.",
                    includeTerraform ? null : "Say 'include Terraform best practices' to see Infrastructure as Code recommendations."
                }.Where(s => s != null)
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting best practices for resource type: {ResourceType}", resourceType);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Error getting resource best practices: {ex.Message}",
                resourceType = resourceType
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    #endregion

    #region Azure Documentation Search (Enhanced)

    [KernelFunction("get_azure_documentation_guidance")]
    [Description("Provide Azure documentation guidance, best practices, and direct links to official Microsoft Learn documentation. " +
                 "Use this when users ask to 'search Azure docs', 'find Azure documentation', or need Azure-specific technical information. " +
                 "Examples: 'Search Azure docs for AKS private cluster', 'Find documentation on Azure Storage encryption', 'How to configure Azure Firewall rules'")]
    public async Task<string> GetAzureDocumentationGuidanceAsync(
        [Description("Search query or topic (e.g., 'AKS private cluster networking', 'Storage account encryption', 'App Service custom domains')")]
        string searchQuery,
        [Description("Optional: Specific Azure service to focus on (e.g., 'AKS', 'Storage', 'App Service', 'Key Vault')")]
        string? azureService = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔍 Generating Azure documentation guidance for: {Query}", searchQuery);

            // Initialize Azure MCP client for live resource queries
            await _azureMcpClient.InitializeAsync(cancellationToken);

            // Build comprehensive documentation response
            var response = new StringBuilder();
            response.AppendLine($"# 📚 Azure Documentation - {searchQuery}");
            response.AppendLine();

            // Determine the service and provide specific documentation
            var serviceLower = azureService?.ToLowerInvariant() ?? "";
            var queryLower = searchQuery.ToLowerInvariant();

            // Detect service from query if not explicitly provided
            if (string.IsNullOrEmpty(serviceLower))
            {
                if (queryLower.Contains("aks") || queryLower.Contains("kubernetes"))
                    serviceLower = "aks";
                else if (queryLower.Contains("storage") || queryLower.Contains("blob"))
                    serviceLower = "storage";
                else if (queryLower.Contains("app service") || queryLower.Contains("web app"))
                    serviceLower = "app service";
                else if (queryLower.Contains("key vault") || queryLower.Contains("keyvault"))
                    serviceLower = "key vault";
                else if (queryLower.Contains("sql") || queryLower.Contains("database"))
                    serviceLower = "sql";
                else if (queryLower.Contains("functions") || queryLower.Contains("function app"))
                    serviceLower = "functions";
                else if (queryLower.Contains("cosmos"))
                    serviceLower = "cosmos";
                else if (queryLower.Contains("firewall"))
                    serviceLower = "firewall";
                else if (queryLower.Contains("virtual network") || queryLower.Contains("vnet"))
                    serviceLower = "vnet";
            }

            // Query live Azure resources using service-specific MCP tools
            string? liveResourceInfo = null;
            try
            {
                liveResourceInfo = await GetLiveResourceInfoAsync(serviceLower, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve live Azure resource information for {Service}", serviceLower);
            }

            // Provide service-specific documentation
            switch (serviceLower)
            {
                case "aks":
                case "kubernetes":
                    response.AppendLine("## Azure Kubernetes Service (AKS)");
                    response.AppendLine();
                    
                    // Add live resource information if available
                    if (!string.IsNullOrEmpty(liveResourceInfo))
                    {
                        response.AppendLine("### 🔴 Your AKS Clusters");
                        response.AppendLine();
                        response.AppendLine(liveResourceInfo);
                        response.AppendLine();
                    }
                    
                    if (queryLower.Contains("private") || queryLower.Contains("networking") || queryLower.Contains("network"))
                    {
                        response.AppendLine("### Private Cluster Networking");
                        response.AppendLine();
                        response.AppendLine("**Private AKS clusters** restrict API server access to private IP addresses for enhanced security.");
                        response.AppendLine();
                        response.AppendLine("**Key Concepts:**");
                        response.AppendLine("- API server endpoint is only accessible from private network");
                        response.AppendLine("- Requires Azure Private Link for connectivity");
                        response.AppendLine("- DNS resolution through private DNS zones");
                        response.AppendLine("- Compatible with Azure CNI or kubenet networking");
                        response.AppendLine();
                        response.AppendLine("**Network Configuration:**");
                        response.AppendLine("- **Azure CNI**: Pods get IPs from VNet subnet");
                        response.AppendLine("- **Kubenet**: Pods use NAT, nodes get VNet IPs");
                        response.AppendLine("- **Network Policies**: Calico or Azure Network Policies");
                        response.AppendLine("- **Ingress**: Application Gateway, NGINX, or Traefik");
                        response.AppendLine();
                        response.AppendLine("**Private Cluster Setup:**");
                        response.AppendLine("```bash");
                        response.AppendLine("# Create private AKS cluster");
                        response.AppendLine("az aks create \\");
                        response.AppendLine("  --resource-group myResourceGroup \\");
                        response.AppendLine("  --name myPrivateCluster \\");
                        response.AppendLine("  --enable-private-cluster \\");
                        response.AppendLine("  --network-plugin azure \\");
                        response.AppendLine("  --vnet-subnet-id <subnet-id>");
                        response.AppendLine("```");
                        response.AppendLine();
                    }
                    
                    response.AppendLine("### Official Documentation");
                    response.AppendLine("- [AKS Documentation](https://learn.microsoft.com/en-us/azure/aks/)");
                    response.AppendLine("- [AKS Networking Concepts](https://learn.microsoft.com/en-us/azure/aks/concepts-network)");
                    response.AppendLine("- [Private AKS Clusters](https://learn.microsoft.com/en-us/azure/aks/private-clusters)");
                    response.AppendLine("- [Azure CNI Networking](https://learn.microsoft.com/en-us/azure/aks/configure-azure-cni)");
                    response.AppendLine("- [AKS Best Practices](https://learn.microsoft.com/en-us/azure/aks/best-practices)");
                    break;

                case "storage":
                    response.AppendLine("## Azure Storage");
                    response.AppendLine();
                    
                    // Add live resource information if available
                    if (!string.IsNullOrEmpty(liveResourceInfo))
                    {
                        response.AppendLine("### 💾 Your Storage Accounts");
                        response.AppendLine();
                        response.AppendLine(liveResourceInfo);
                        response.AppendLine();
                    }
                    
                    response.AppendLine("### Official Documentation");
                    response.AppendLine("- [Azure Storage Documentation](https://learn.microsoft.com/en-us/azure/storage/)");
                    response.AppendLine("- [Storage Security Guide](https://learn.microsoft.com/en-us/azure/storage/common/storage-security-guide)");
                    response.AppendLine("- [Blob Storage](https://learn.microsoft.com/en-us/azure/storage/blobs/)");
                    response.AppendLine("- [Storage Encryption](https://learn.microsoft.com/en-us/azure/storage/common/storage-service-encryption)");
                    break;

                case "app service":
                    response.AppendLine("## Azure App Service");
                    response.AppendLine();
                    
                    // Add live resource information if available
                    if (!string.IsNullOrEmpty(liveResourceInfo))
                    {
                        response.AppendLine("### 🕸️ Your App Services");
                        response.AppendLine();
                        response.AppendLine(liveResourceInfo);
                        response.AppendLine();
                    }
                    
                    response.AppendLine("### Official Documentation");
                    response.AppendLine("- [App Service Documentation](https://learn.microsoft.com/en-us/azure/app-service/)");
                    response.AppendLine("- [App Service Best Practices](https://learn.microsoft.com/en-us/azure/app-service/app-service-best-practices)");
                    response.AppendLine("- [Custom Domains](https://learn.microsoft.com/en-us/azure/app-service/app-service-web-tutorial-custom-domain)");
                    break;

                case "key vault":
                    response.AppendLine("## Azure Key Vault");
                    response.AppendLine();
                    
                    // Add live resource information if available
                    if (!string.IsNullOrEmpty(liveResourceInfo))
                    {
                        response.AppendLine("### 🔑 Your Key Vaults");
                        response.AppendLine();
                        response.AppendLine(liveResourceInfo);
                        response.AppendLine();
                    }
                    
                    response.AppendLine("### Official Documentation");
                    response.AppendLine("- [Key Vault Documentation](https://learn.microsoft.com/en-us/azure/key-vault/)");
                    response.AppendLine("- [Key Vault Security](https://learn.microsoft.com/en-us/azure/key-vault/general/security-features)");
                    response.AppendLine("- [Managed Identities](https://learn.microsoft.com/en-us/azure/key-vault/general/managed-identity)");
                    break;

                case "sql":
                    response.AppendLine("## Azure SQL Database");
                    response.AppendLine();
                    
                    // Add live resource information if available
                    if (!string.IsNullOrEmpty(liveResourceInfo))
                    {
                        response.AppendLine("### 🗄️ Your SQL Servers");
                        response.AppendLine();
                        response.AppendLine(liveResourceInfo);
                        response.AppendLine();
                    }
                    
                    response.AppendLine("### Official Documentation");
                    response.AppendLine("- [Azure SQL Documentation](https://learn.microsoft.com/en-us/azure/azure-sql/)");
                    response.AppendLine("- [SQL Security](https://learn.microsoft.com/en-us/azure/azure-sql/database/security-overview)");
                    break;

                case "cosmos":
                    response.AppendLine("## Azure Cosmos DB");
                    response.AppendLine();
                    
                    // Add live resource information if available
                    if (!string.IsNullOrEmpty(liveResourceInfo))
                    {
                        response.AppendLine("### 📊 Your Cosmos DB Accounts");
                        response.AppendLine();
                        response.AppendLine(liveResourceInfo);
                        response.AppendLine();
                    }
                    
                    response.AppendLine("### Official Documentation");
                    response.AppendLine("- [Cosmos DB Documentation](https://learn.microsoft.com/en-us/azure/cosmos-db/)");
                    response.AppendLine("- [Cosmos DB Best Practices](https://learn.microsoft.com/en-us/azure/cosmos-db/best-practice-guide)");
                    break;

                default:
                    response.AppendLine("## Azure Documentation Resources");
                    response.AppendLine();
                    
                    // Show general subscription info if available
                    if (!string.IsNullOrEmpty(liveResourceInfo))
                    {
                        response.AppendLine("### Your Azure Resources");
                        response.AppendLine();
                        response.AppendLine(liveResourceInfo);
                        response.AppendLine();
                    }
                    
                    response.AppendLine("### General Resources");
                    response.AppendLine("- [Azure Documentation Home](https://learn.microsoft.com/en-us/azure/)");
                    response.AppendLine("- [Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/)");
                    response.AppendLine("- [Well-Architected Framework](https://learn.microsoft.com/en-us/azure/well-architected/)");
                    response.AppendLine("- [Azure QuickStart Templates](https://github.com/Azure/azure-quickstart-templates)");
                    break;
            }

            response.AppendLine();
            response.AppendLine("---");
            response.AppendLine();
            response.AppendLine("💡 **Need more specific guidance?** Ask about:");
            response.AppendLine("- Configuration steps for your scenario");
            response.AppendLine("- Best practices and security recommendations");
            response.AppendLine("- Code examples and implementation details");
            response.AppendLine("- Architecture patterns and design decisions");

            _logger.LogInformation("✅ Azure documentation guidance generated successfully");
            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Azure documentation guidance");
            return $@"❌ **Error Accessing Azure Documentation**

An error occurred while generating documentation guidance for: {searchQuery}

**Error:** {ex.Message}

**Please try:**
1. Visit [Azure Documentation](https://learn.microsoft.com/en-us/azure/) directly
2. Search [Microsoft Learn](https://learn.microsoft.com/) for your topic
3. Ask me for more specific configuration guidance

If you need help with a specific Azure service, let me know!";
        }
    }

    /// <summary>
    /// Get live Azure resource information using service-specific MCP tools
    /// </summary>
    private async Task<string?> GetLiveResourceInfoAsync(string service, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Querying live Azure resources for service: {Service}", service);
            
            var result = service switch
            {
                "aks" or "kubernetes" => await GetAksResourcesAsync(cancellationToken),
                "storage" => await GetStorageResourcesAsync(cancellationToken),
                "app service" => await GetAppServiceResourcesAsync(cancellationToken),
                "key vault" => await GetKeyVaultResourcesAsync(cancellationToken),
                "sql" => await GetSqlResourcesAsync(cancellationToken),
                "cosmos" => await GetCosmosResourcesAsync(cancellationToken),
                _ => null
            };

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve live resources for {Service}", service);
            return null;
        }
    }

    private async Task<string?> GetAksResourcesAsync(CancellationToken cancellationToken)
    {
        var result = await _azureMcpClient.CallToolAsync("aks", new Dictionary<string, object?>(), cancellationToken);
        
        if (result.Success && result.Result != null)
        {
            var response = new StringBuilder();
            response.AppendLine("```");
            response.AppendLine(result.Result.ToString());
            response.AppendLine("```");
            return response.ToString();
        }
        
        return null;
    }

    private async Task<string?> GetStorageResourcesAsync(CancellationToken cancellationToken)
    {
        var result = await _azureMcpClient.CallToolAsync("storage", new Dictionary<string, object?>(), cancellationToken);
        
        if (result.Success && result.Result != null)
        {
            var response = new StringBuilder();
            response.AppendLine("```");
            response.AppendLine(result.Result.ToString());
            response.AppendLine("```");
            return response.ToString();
        }
        
        return null;
    }

    private async Task<string?> GetAppServiceResourcesAsync(CancellationToken cancellationToken)
    {
        var result = await _azureMcpClient.CallToolAsync("appservice", new Dictionary<string, object?>(), cancellationToken);
        
        if (result.Success && result.Result != null)
        {
            var response = new StringBuilder();
            response.AppendLine("```");
            response.AppendLine(result.Result.ToString());
            response.AppendLine("```");
            return response.ToString();
        }
        
        return null;
    }

    private async Task<string?> GetKeyVaultResourcesAsync(CancellationToken cancellationToken)
    {
        var result = await _azureMcpClient.CallToolAsync("keyvault", new Dictionary<string, object?>(), cancellationToken);
        
        if (result.Success && result.Result != null)
        {
            var response = new StringBuilder();
            response.AppendLine("```");
            response.AppendLine(result.Result.ToString());
            response.AppendLine("```");
            return response.ToString();
        }
        
        return null;
    }

    private async Task<string?> GetSqlResourcesAsync(CancellationToken cancellationToken)
    {
        var result = await _azureMcpClient.CallToolAsync("sql", new Dictionary<string, object?>(), cancellationToken);
        
        if (result.Success && result.Result != null)
        {
            var response = new StringBuilder();
            response.AppendLine("```");
            response.AppendLine(result.Result.ToString());
            response.AppendLine("```");
            return response.ToString();
        }
        
        return null;
    }

    private async Task<string?> GetCosmosResourcesAsync(CancellationToken cancellationToken)
    {
        var result = await _azureMcpClient.CallToolAsync("cosmos", new Dictionary<string, object?>(), cancellationToken);
        
        if (result.Success && result.Result != null)
        {
            var response = new StringBuilder();
            response.AppendLine("```");
            response.AppendLine(result.Result.ToString());
            response.AppendLine("```");
            return response.ToString();
        }
        
        return null;
    }

    #endregion
}
