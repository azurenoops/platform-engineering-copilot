# GitHub Copilot Extension — Platform Engineering Copilot

This directory contains the scaffold for the GitHub Copilot Extension that integrates Platform Copilot as a `@platform` chat participant.

## Structure

```
GitHub/
├── README.md                  # This file
├── manifest.json              # Copilot extension manifest
├── PlatformParticipant.cs     # Chat participant implementation
└── InlineComplianceChecker.cs # Inline compliance checking (FR-064)
```

## Features (FR-064)

- **@platform chat participant**: Users type `@platform` in GitHub Copilot Chat to interact with the Platform Copilot
- **Inline compliance checking**: Real-time compliance annotations on IaC files (Bicep, Terraform, ARM)
- **Control explanations**: Hover tooltips with NIST 800-53 control descriptions
- **Assessment triggers**: Run compliance assessments directly from the editor

## Integration

The extension communicates with the Platform Copilot MCP server via stdio transport:
```
@platform assess my environment against FedRAMP High
@platform explain control AC-2
@platform generate a compliant AKS template
```

## Status

**Scaffold only** — Full implementation requires GitHub Copilot Extension SDK GA release.
