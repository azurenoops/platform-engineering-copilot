# Document Generation - Quick Start Guide

## Overview
The Document Generation Service provides automated creation of ATO compliance documents including System Security Plans (SSP), Security Assessment Reports (SAR), and Plans of Action & Milestones (POA&M).

## Available Through
- **Compliance Agent** - Natural language interface via chat
- **Document Generation Plugin** - Semantic Kernel plugin functions
- **Direct Service** - IDocumentGenerationService dependency injection

## Quick Examples

### 1. Generate Control Narrative

**Via Chat:**
```
Generate a control narrative for AC-2 (Account Management) for subscription abc-123
```

**Via Plugin:**
```csharp
var narrative = await documentPlugin.GenerateControlNarrativeAsync(
    controlId: "AC-2",
    subscriptionId: "abc-123"
);
```

**Expected Output:**
- Control ID and title
- Implementation status
- What/How descriptions
- Customer responsibilities
- Azure inherited capabilities
- Evidence artifacts
- Compliance status

### 2. Generate System Security Plan (SSP)

**Via Chat:**
```
Generate an SSP for subscription abc-123 with system name "Azure Gov Platform", 
impact level IL4, classified as UNCLASSIFIED
```

**Via Plugin:**
```csharp
var parameters = new Dictionary<string, string>
{
    ["subscriptionId"] = "abc-123",
    ["systemName"] = "Azure Gov Platform",
    ["systemDescription"] = "Cloud platform for government workloads",
    ["impactLevel"] = "IL4",
    ["classification"] = "UNCLASSIFIED",
    ["systemOwner"] = "Platform Engineering",
    ["authorizingOfficial"] = "John Doe"
};

var result = await documentPlugin.GenerateDocumentFromTemplateAsync(
    templateType: "SSP",
    parameters: JsonSerializer.Serialize(parameters)
);
```

**Expected Output:**
- Complete SSP document (typically 85+ pages)
- All 18 NIST 800-53 control families
- Executive summary with compliance score
- System categorization
- Control implementation narratives
- Document ID for export

### 3. Generate Security Assessment Report (SAR)

**Via Chat:**
```
Generate a SAR for subscription abc-123 using assessment assessment-2024-001
```

**Via Plugin:**
```csharp
var parameters = new Dictionary<string, string>
{
    ["subscriptionId"] = "abc-123",
    ["assessmentId"] = "assessment-2024-001"
};

var result = await documentPlugin.GenerateDocumentFromTemplateAsync(
    templateType: "SAR",
    parameters: JsonSerializer.Serialize(parameters)
);
```

**Expected Output:**
- Security Assessment Report (typically 40+ pages)
- Overall compliance score
- Control family assessment results
- Detailed findings with severity levels
- Resource-level compliance status

### 4. Generate Plan of Action & Milestones (POA&M)

**Via Chat:**
```
Generate a POA&M for subscription abc-123
```

**Via Plugin:**
```csharp
var parameters = new Dictionary<string, string>
{
    ["subscriptionId"] = "abc-123"
};

var result = await documentPlugin.GenerateDocumentFromTemplateAsync(
    templateType: "POAM",
    parameters: JsonSerializer.Serialize(parameters)
);
```

**Expected Output:**
- Tabular POA&M format
- All findings with severity levels
- Automated target dates (30/90/180 days based on severity)
- Affected NIST controls
- Remediation actions
- Priority categorization

### 5. Export Documents

**Via Chat:**
```
Export document SSP-abc123 as HTML
```

**Via Plugin:**
```csharp
var result = await documentPlugin.ExportDocumentAsync(
    documentId: "SSP-abc123",
    format: "html"
);
```

**Supported Formats:**
- ✅ `markdown` - Plain text Markdown (fully functional)
- ✅ `html` - HTML with CSS styling (fully functional)
- 🔄 `docx` - Microsoft Word (coming soon)
- 🔄 `pdf` - PDF format (coming soon)

### 6. Apply Formatting Standards

**Via Chat:**
```
Format document SSP-abc123 with FedRAMP standard
```

**Via Plugin:**
```csharp
var result = await documentPlugin.FormatDocumentAsync(
    documentId: "SSP-abc123",
    standard: "FedRAMP"
);
```

**Supported Standards:**
- `NIST` - NIST SP 800-53 Rev 5
- `FedRAMP` - FedRAMP Rev 5 (2023)
- `DoD_RMF` or `DoD-RMF` - DoD RMF v2.0
- `FISMA` - FISMA compliance

### 7. List Documents

**Via Chat:**
```
List all documents in ATO package for subscription abc-123
```

**Via Plugin:**
```csharp
var result = await documentPlugin.ListDocumentsAsync(
    packageId: "abc-123"
);
```

## Using the Service Directly

### Setup Dependency Injection

```csharp
// In your service configuration
services.AddComplianceAgent(configuration);

// The DocumentGenerationService is automatically registered
```

### Inject and Use

```csharp
public class MyService
{
    private readonly IDocumentGenerationService _documentService;
    
    public MyService(IDocumentGenerationService documentService)
    {
        _documentService = documentService;
    }
    
    public async Task<GeneratedDocument> CreateSSP(string subscriptionId)
    {
        var parameters = new SspParameters
        {
            SystemName = "My System",
            SystemDescription = "System description",
            ImpactLevel = "IL4",
            Classification = "UNCLASSIFIED"
        };
        
        return await _documentService.GenerateSSPAsync(
            subscriptionId, 
            parameters);
    }
}
```

## Document Structure

### Generated Documents Include:
- **DocumentId** - Unique identifier (GUID)
- **DocumentType** - SSP, SAR, or POAM
- **Title** - Document title
- **Version** - Document version (default: "1.0")
- **GeneratedDate** - UTC timestamp
- **Classification** - Security classification
- **Content** - Full document content (Markdown format)
- **Sections** - Hierarchical document sections
- **Metadata** - Additional key-value pairs

### SSP Sections (18 Control Families):
1. AC - Access Control
2. AT - Awareness and Training
3. AU - Audit and Accountability
4. CA - Security Assessment and Authorization
5. CM - Configuration Management
6. CP - Contingency Planning
7. IA - Identification and Authentication
8. IR - Incident Response
9. MA - Maintenance
10. MP - Media Protection
11. PE - Physical and Environmental Protection
12. PL - Planning
13. PM - Program Management
14. PS - Personnel Security
15. RA - Risk Assessment
16. SA - System and Services Acquisition
17. SC - System and Communications Protection
18. SI - System and Information Integrity

## Testing

Integration tests are available at:
```
tests/Platform.Engineering.Copilot.Tests.Integration/DocumentGenerationTests.cs
```

Run specific tests:
```bash
dotnet test --filter "FullyQualifiedName~DocumentGenerationTests"
```

Note: Tests are marked with `Skip` attribute and require manual execution with real Azure subscriptions.

## Troubleshooting

### Common Issues

**1. "Document not found" error**
- Ensure the documentId exists
- Check that the subscription has been assessed
- Verify you have access to the subscription

**2. "NotImplementedException" on export**
- DOCX and PDF export are not yet implemented
- Use Markdown or HTML formats currently

**3. Empty or minimal content**
- Run a compliance assessment first using IAtoComplianceEngine
- Ensure the subscription has Azure resources
- Check that NIST controls service is available

**4. Missing control families**
- Verify the NIST controls catalog is loaded
- Check NistControlsService configuration
- Ensure network access to NIST catalog source

## Next Steps

1. **Enhance Evidence Collection**: Link actual Azure resource configurations
2. **DOCX Export**: Implement using DocumentFormat.OpenXml
3. **PDF Export**: Implement using iTextSharp or similar
4. **Storage Integration**: Persist documents to Azure Blob Storage
5. **Version Control**: Track document revisions and changes
6. **Collaboration**: Add multi-user editing capabilities
7. **Digital Signatures**: Enable ATO package signing

## Related Documentation

- [DOCUMENT-GENERATION-IMPLEMENTATION.md](./DOCUMENT-GENERATION-IMPLEMENTATION.md) - Detailed implementation plan
- [DOCUMENT-GENERATION-SUMMARY.md](./DOCUMENT-GENERATION-SUMMARY.md) - Service implementation overview
- [DOCUMENT-PLUGIN-UPDATE.md](./DOCUMENT-PLUGIN-UPDATE.md) - Plugin integration details
- [REPOSITORY-SCANNING-GUIDE.md](./REPOSITORY-SCANNING-GUIDE.md) - Code scanning for security

## Support

For issues or questions:
1. Check the documentation in `docs/Compliance Agent/`
2. Review test cases for usage examples
3. Enable debug logging for detailed diagnostics
4. Contact the Platform Engineering team
