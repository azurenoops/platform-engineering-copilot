<!--
  SYNC IMPACT REPORT
  ==================
  Version change: 0.0.0 → 1.0.0 (initial ratification)
  
  Added principles:
    - I. Documentation as Source of Truth
    - II. BaseAgent/BaseTool Architecture
    - III. Test-First Development (NON-NEGOTIABLE)
    - IV. Azure Government & Compliance First
    - V. Observability & Structured Logging
  
  Added sections:
    - Azure Government & Compliance Requirements
    - Development Workflow & Quality Gates
    - Governance
  
  Templates requiring updates:
    ✅ .specify/templates/plan-template.md - Constitution Check section aligned
    ✅ .specify/templates/spec-template.md - Requirements format compatible
    ✅ .specify/templates/tasks-template.md - Test phases aligned
  
  Follow-up TODOs: None
-->

# Platform Engineering Copilot Constitution

## Core Principles

### I. Documentation as Source of Truth

All design and code changes MUST follow guidance documented under `/docs/`. If guidance conflicts
with "general best practices," repo guidance wins. Missing guidance MUST NOT be invented; instead,
propose an ADR or new document in `/docs/standards`.

**Rationale**: Ensures consistency across contributors and AI-assisted development. Every
recommendation MUST cite the doc path + section heading. If no rule exists, state so and suggest
where it should live.

### II. BaseAgent/BaseTool Architecture

All agents MUST extend `BaseAgent`. All tools MUST extend `BaseTool`. This pattern is
NON-NEGOTIABLE and applies to every feature touching the agent layer.

- Agents MUST implement: `AgentId`, `AgentName`, `Description`, `GetSystemPrompt()`
- Tools MUST implement: `Name`, `Description`, `Parameters`, `ExecuteAsync()`
- System prompts MUST be externalized in `*.prompt.txt` files
- Tools MUST be registered via `RegisterTool()` in agent constructors

**Rationale**: Standardized abstractions enable multi-agent orchestration, consistent function
calling, and maintainable code. Found in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#baseagenttool-framework).

### III. Test-First Development (NON-NEGOTIABLE)

All behavior changes MUST include corresponding test changes. Target coverage: 80%+ for unit tests.

- **Unit Tests** (`Tests.Unit/`): Isolated component tests with mocked dependencies using xUnit,
  FluentAssertions, and Moq
- **Integration Tests** (`Tests.Integration/`): WebApplicationFactory-based API and database tests
- **Manual Tests** (`Tests.Manual/`): Documented verification scenarios for complex workflows

Build/test discipline: Every change proposal MUST include exact `dotnet build` and `dotnet test`
commands with expected outcomes.

**Rationale**: Tests are the contract. Code without tests is incomplete. Found in
[docs/DEVELOPMENT.md](docs/DEVELOPMENT.md#test-layer-platformengineeringcopilotstests).

### IV. Azure Government & Compliance First

This platform targets Azure Government with NIST 800-53 compliance. All Azure interactions MUST:

- Support dual cloud environments: `AzureUSGovernment` (primary) and `AzureCloud`
- Use `DefaultAzureCredential` chain (Azure CLI for dev, Managed Identity for prod)
- Never hardcode credentials; use Key Vault or environment variables
- Validate against FedRAMP High and NIST 800-53 control families where applicable

**Rationale**: Government workloads require strict compliance boundaries. Authentication patterns
documented in [docs/AUTHENTICATION.md](docs/AUTHENTICATION.md).

### V. Observability & Structured Logging

All services MUST implement structured logging with Serilog. Logging requirements:

- Console + file sinks MUST be configured for development
- Application Insights sink MUST be configured for production
- Tool executions MUST log: input parameters, execution duration, success/failure
- Agent invocations MUST log: selected agent, tool chain, termination reason

**Rationale**: Multi-agent systems require traceability. Logs enable debugging agent selection,
tool execution chains, and compliance auditing.

## Azure Government & Compliance Requirements

All code interacting with Azure services MUST adhere to:

| Requirement | Standard |
|-------------|----------|
| Authentication | Managed Identity (prod), Azure CLI (dev) |
| Secrets | Azure Key Vault only; no hardcoded values |
| Networking | Private endpoints preferred; firewall rules documented |
| Compliance | NIST 800-53 control mapping for security-relevant features |
| Data Residency | US regions only (usgovvirginia, usgovarizona, usgovtexas) |

Infrastructure code (Bicep/Terraform) MUST include compliance annotations and policy assignments.

## Development Workflow & Quality Gates

### Required Output Format

For any change proposal, output:

1. **Guidance Compliance Report**: PASS/FAIL with rule-by-rule citations
2. **Architecture Decision**: If architecture/design is impacted, document rationale
3. **Code Changes**: Files changed + why + build/test commands + rollback procedure

### Quality Gates

| Gate | Requirement |
|------|-------------|
| Build | `dotnet build Platform.Engineering.Copilot.sln` MUST pass |
| Unit Tests | `dotnet test` MUST pass with 80%+ coverage |
| Linting | No new warnings in modified files |
| Documentation | New features MUST update relevant `/docs/*.md` |

### Branch Strategy

- Feature branches: `###-feature-name` format (e.g., `042-add-security-agent`)
- All changes via pull request with minimum 1 reviewer
- CI pipeline MUST pass before merge

## Governance

This constitution supersedes all other development practices for the Platform Engineering Copilot
project. Amendments follow this process:

1. **Proposal**: Open issue or PR with proposed change and rationale
2. **Review**: Minimum 1 maintainer approval required
3. **Migration**: Breaking changes MUST include migration plan
4. **Version Bump**: Follow semantic versioning (MAJOR.MINOR.PATCH)

All PRs and code reviews MUST verify compliance with this constitution. Complexity beyond these
principles MUST be explicitly justified in the PR description.

Use [.github/copilot-instructions.md](.github/copilot-instructions.md) for runtime AI development
guidance.

**Version**: 1.0.0 | **Ratified**: 2026-02-21 | **Last Amended**: 2026-02-21
