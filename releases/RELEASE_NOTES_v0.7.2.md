# Release Notes - v0.7.2

**Release Date:** November 21, 2025  
**Type:** Feature Release

## 🎯 Overview

Version 0.7.2 introduces enhanced compliance capabilities, improved infrastructure template generation, and better integration with Azure services. This release focuses on improving the compliance agent's evidence collection and remediation planning features.

## ✨ New Features

### Compliance Agent Enhancements
- **Enhanced Evidence Collection**: Improved evidence collection across all NIST 800-53 control families (AC, AU, CM, SC, IA, SI)
- **Multi-Format Evidence Packages**: Evidence now exported in JSON, CSV, and eMASS XML formats for better integration
- **Automated Remediation Planning**: Generate comprehensive remediation plans with effort estimates and priority classifications
- **Subscription-Scoped Evidence**: Collect compliance evidence scoped to specific resource groups
- **Improved Completeness Scoring**: Better visibility into evidence collection completeness per control family

### Infrastructure Agent Improvements
- **AKS Template Generation**: Enhanced Bicep template generation for Azure Kubernetes Service with compliance controls
- **Multi-Module Architecture**: Generated templates now include modular components (networking, security, monitoring, identity)
- **Government Cloud Support**: Improved support for usgovvirginia and other Azure Government regions
- **Network Policy Templates**: Auto-generated Kubernetes network policies and pod security policies

### Document Generation
- **Real-Time Evidence Integration**: SSP generation now includes real-time evidence collection with performance metrics
- **Evidence Package Metadata**: Enhanced document metadata including evidence package IDs, collection duration, and URI references
- **Azure Blob Storage Integration**: Evidence packages automatically stored in Azure Blob Storage with versioning and immutability support

## 🔧 Improvements

### Performance
- **Evidence Collection Optimization**: Reduced evidence collection time through parallel processing
- **NIST Controls Caching**: 24-hour cache for NIST control catalog with offline fallback support
- **Defender for Cloud Integration**: Optional integration with 60-minute cache duration and deduplication

### Configuration
- **Flexible Storage Configuration**: Configurable evidence retention (default: 2555 days) with versioning and immutability options
- **Enhanced Logging**: Detailed logging options for NIST controls and evidence collection processes
- **Retry Logic**: Improved retry mechanisms with configurable attempts and delays

### User Experience
- **Clear Status Indicators**: Visual status indicators (✅/❌) for evidence collection and compliance assessments
- **Actionable Next Steps**: Automated recommendations for addressing compliance gaps
- **Progress Tracking**: Better visibility into multi-step operations with intermediate status updates

## 🐛 Bug Fixes

- Fixed module filtering in infrastructure template generation
- Improved error handling for missing NIST control families
- Corrected evidence package generation for incomplete control families
- Enhanced error messages for failed evidence collection

## 📋 Configuration Changes

### Updated `appsettings.json` Parameters

```json
{
  "ComplianceAgent": {
    "Evidence": {
      "StorageAccount": "mcpsa",
      "Container": "evidence",
      "RetentionDays": 2555,
      "EnableVersioning": true,
      "EnableImmutability": true
    },
    "NistControls": {
      "TimeoutSeconds": 60,
      "CacheDurationHours": 24,
      "MaxRetryAttempts": 3,
      "RetryDelaySeconds": 2,
      "EnableOfflineFallback": true,
      "EnableDetailedLogging": false
    }
  }
}
```

## 🔒 Security

- Evidence packages stored with versioning and immutability protection
- Secrets detection enabled in code scanning
- Enhanced RBAC controls in generated AKS templates
- TLS enforcement in compliance-aware templates

## 📦 Dependencies

- .NET 8.0+
- Azure SDK latest versions
- Docker Compose for MCP server deployment
- Azure Government cloud compatibility

## 🚀 Deployment Notes

### Breaking Changes
- None in this release

### Migration Steps
1. Update `appsettings.json` with new evidence storage configuration
2. Ensure Azure Storage Account is configured for evidence storage
3. Review and update NIST controls cache settings if needed
4. Restart MCP server: `docker-compose restart platform-mcp`

### Testing Recommendations
- Verify evidence collection for all control families
- Test remediation plan generation with your subscription
- Validate Bicep template generation for AKS clusters
- Confirm evidence package storage in Azure Blob Storage

## 📚 Documentation Updates

- Updated compliance agent test suite with evidence collection examples
- Enhanced infrastructure agent documentation with template generation workflows
- Added document generation quickstart guide
- Improved CAC authentication documentation

## 🔜 Coming Soon (v0.8.0)

- Environment Agent activation
- Security Agent enhancements
- Service Creation Agent integration
- Document Agent implementation
- Enhanced M365 extension integration
- Advanced cost optimization recommendations

## 📞 Support

For issues, feature requests, or questions:
- GitHub Issues: https://github.com/azurenoops/platform-engineering-copilot/issues
- Documentation: `/docs/README.md`

## 🙏 Contributors

Thank you to all contributors who made this release possible!

---

**Full Changelog**: v0.7.1...v0.7.2
