# Platform Engineering Copilot

> **AI-Powered Infrastructure & Compliance Platform for Azure Government**

The Platform Engineering Copilot is an enterprise platform combining AI agents with Azure resource management, NIST 800-53 compliance automation, and cost optimization. Built on .NET 9.0 and Microsoft Semantic Kernel with Model Context Protocol (MCP) server architecture.

## 🌟 Overview

The Platform Engineering Copilot transforms cloud infrastructure management through natural language interfaces and specialized AI agents.

**6 Specialized AI Agents:**
1. **Infrastructure** - Azure provisioning, IaC generation (Terraform/Bicep)
2. **Cost Optimization** - Analysis, budgets, savings recommendations
3. **Compliance** - NIST 800-53 scanning, gap analysis, remediation
4. **Security** - Vulnerability scanning, policy enforcement
5. **Document** - ATO artifact generation (SSP, SAR, POAM)
6. **ATO Preparation** - End-to-end package orchestration

**Dual-Mode MCP Server:**
- **HTTP Mode** (port 5100): Web applications, REST API
- **stdio Mode**: GitHub Copilot, Claude Desktop integration


## 🚀 Quick Start

### Prerequisites

- .NET 9.0 SDK
- Docker & Docker Compose
- Azure CLI (**required** for authentication)
- Azure OpenAI (GPT-4o recommended)
- Azure subscription (Government or Commercial)

### 1. Setup

```bash
# Clone Platform Engineering Copilot
git clone https://github.com/azurenoops/platform-engineering-copilot.git
cd platform-engineering-copilot
dotnet build

# Azure authentication
az cloud set --name AzureUSGovernment  # or AzureCloud
az login
export AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)

# Configure environment
cp .env.example .env
# Edit .env with your Azure OpenAI and subscription details
```

### 2. Deploy

**MCP Server Only** (recommended for AI clients):
```bash
docker-compose -f docker-compose.essentials.yml up -d
curl http://localhost:5100/health
```

**Full Platform** (includes web UI):
```bash
docker-compose up -d
open http://localhost:5001  # Chat interface
```

### 3. Try It

```bash
# REST API
curl -X POST http://localhost:5100/api/chat/message \
  -H "Content-Type: application/json" \
  -d '{"sessionId": "test", "message": "List all resource groups"}'

# Example queries
"Create storage account data001 in rg-dr"
"Run NIST 800-53 compliance scan"
"Show cost analysis for last 30 days"
```

**📖 Guides:** [Authentication](./docs/QUICKSTART-AUTHENTICATION.md) • [Docker](./DOCKER-COMPOSE-GUIDE.md) • [Development](./docs/DEVELOPMENT.md)


## 🎯 Key Features

### Multi-Agent Orchestration
**6 specialized agents** with seamless coordination, shared context, and parallel execution for complex workflows.

### Compliance & Governance
- **NIST 800-53 Rev 5**: All 18 control families (1000+ controls)
- **FedRAMP**: High/Moderate/Low baseline validation
- **Gap Analysis**: Automated remediation planning
- **Azure Policy**: Real-time evaluation and enforcement

### Cost Management
- Real-time analysis by service, resource group, location, tags
- Budget tracking and forecasting
- Right-sizing and reserved instance recommendations
- Idle resource identification

### Security
- Vulnerability scanning and threat detection
- Azure Policy integration with approval workflows
- Security posture monitoring
- TLS 1.2+, encryption at rest/transit

### Infrastructure as Code
- Generate Terraform, Bicep, Kubernetes manifests
- Template validation and deployment
- Security defaults for all resources
- Auto-create resource groups with managed tags


## 🏗️ Architecture

```
┌─────────────────────────────────────────┐
│         CLIENTS                         │
│  • Web Chat (5001)                      │
│  • GitHub Copilot / Claude (stdio)      │
│  • Admin Console (5003)                 │
└─────────────────────────────────────────┘
              ↓ HTTP / stdio
┌─────────────────────────────────────────┐
│      MCP SERVER (5100)                  │
│  ├── HTTP Mode (Web Apps)               │
│  ├── stdio Mode (AI Clients)            │
│  └── 6 Specialized Agents               │
└─────────────────────────────────────────┘
              ↓
┌─────────────────────────────────────────┐
│      BUSINESS LOGIC                     │
│  • Semantic Kernel Integration          │
│  • Agent Plugins & Tools                │
│  • Domain Services                      │
└─────────────────────────────────────────┘
              ↓
┌─────────────────────────────────────────┐
│      DATA & EXTERNAL                    │
│  • SQL Server (EF Core 9)               │
│  • Azure Resource Manager               │
│  • Azure OpenAI (GPT-4o)                │
│  • Cost/Policy/Security APIs            │
└─────────────────────────────────────────┘
```

**Technology Stack:**
- .NET 9.0, Semantic Kernel 1.26.0, EF Core 9.0
- Azure OpenAI (GPT-4o), Azure SDK 1.48.0+
- Docker, Terraform, Bicep, Kubernetes


## 📚 Documentation

**Getting Started:**
- [Authentication Quick Start](./docs/QUICKSTART-AUTHENTICATION.md)
- [Docker Deployment](./DOCKER-COMPOSE-GUIDE.md)
- [Development Setup](./docs/DEVELOPMENT.md)

**Integration:**
- [GitHub Copilot](./docs/GITHUB-COPILOT-INTEGRATION.md)
- [M365 Copilot](./docs/M365-COPILOT-INTEGRATION.md)

**Reference:**
- [Architecture](./docs/ARCHITECTURE.md)
- [Test Cases](./docs/NATURAL-LANGUAGE-TEST-CASES.md)
- [LLM Configuration](./docs/GETTING-STARTED.md#llm-configuration--model-requirements)


## 🎯 Use Cases

**Infrastructure Provisioning:**
```
"Create storage account data001 in rg-dr in usgovvirginia"
→ Validates subscription → Creates resource group if needed → Deploys with security defaults
```

**Compliance Assessment:**
```
"Run NIST 800-53 compliance scan"
→ Scans 18 control families → Gap analysis → Remediation plan → POAM generation
```

**Cost Optimization:**
```
"Show cost overview for last 30 days with recommendations"
→ Cost dashboard → Usage analysis → Right-sizing opportunities → Reserved instances
```

**Security Validation:**
```
"Scan infrastructure for vulnerabilities and policy violations"
→ Policy evaluation → Security scan → Findings with severity → Remediation guidance
```

**ATO Documentation:**
```
"Generate System Security Plan for production environment"
→ Collects evidence → Generates SSP, SAR, POAM → Tracks submission
```


##  Deployment Options

**MCP Server Only (Essentials):**
```bash
docker-compose -f docker-compose.essentials.yml up -d
# For AI clients (GitHub Copilot, Claude Desktop)
```

**Full Platform:**
```bash
docker-compose up -d
# Includes web chat, admin console, MCP server
```

**Development with Hot Reload:**
```bash
docker-compose -f docker-compose.essentials.yml -f docker-compose.dev.yml up -d
```

**Production with Scaling:**
```bash
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

**Cloud Deployment:**
- Azure Container Instances, AKS, Container Apps
- See [DEPLOYMENT.md](./docs/DEPLOYMENT.md) for details


## 🔌 Integration

**GitHub Copilot Integration:**

Connect Platform Engineering Copilot to GitHub Copilot for AI-powered infrastructure operations directly in VS Code.

```json
{
  "mcp": {
    "servers": {
      "platform-engineering-copilot": {
        "command": "docker",
        "args": ["exec", "-i", "plaform-engineering-copilot-mcp", 
                 "dotnet", "run", "--project", "/app/Platform.Engineering.Copilot.Mcp.csproj"]
      }
    }
  }
}
```
[Setup Guide](./docs/GITHUB-COPILOT-INTEGRATION.md)

**Claude Desktop Integration:**

Use Platform Engineering Copilot with Claude Desktop for AI-assisted infrastructure management.

```json
{
  "mcpServers": {
    "platform-engineering-copilot": {
      "command": "docker",
      "args": ["exec", "-i", "plaform-engineering-copilot-mcp", ...]
    }
  }
}
```

**Web Chat Interface:**

Use the built-in web chat to interact with Platform Engineering Copilot.

```bash
docker-compose up -d
open http://localhost:5001
```

---

## 📄 License

MIT License - Copyright © 2025 Microsoft Federal

---

## 🔗 Resources

- [Azure Government](https://docs.microsoft.com/en-us/azure/azure-government/)
- [NIST 800-53 Rev 5](https://csrc.nist.gov/publications/detail/sp/800-53/rev-5/final)
- [FedRAMP](https://www.fedramp.gov/)
- [Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/)
- [Model Context Protocol](https://modelcontextprotocol.io/)

---

**Version**: 0.6.35  
**Last Updated**: November 11, 2025  
**Team**: Microsoft Federal CSU Platform Engineering

## 🤝 Contributing

Contributions welcome! Please:
1. Read the [ARCHITECTURE.md](./docs/ARCHITECTURE.md) guide
2. Check existing [documentation](./docs/DEVELOPMENT.md)
3. Submit pull requests with tests
4. Follow existing code patterns

---

## 📄 License

Copyright © 2025 Microsoft Federal

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🔗 Resources

- **[Azure Government Documentation](https://docs.microsoft.com/en-us/azure/azure-government/)**
- **[NIST 800-53 Rev 5](https://csrc.nist.gov/publications/detail/sp/800-53/rev-5/final)**
- **[RMF Framework](https://csrc.nist.gov/projects/risk-management/about-rmf)**
- **[FedRAMP Compliance](https://www.fedramp.gov/)**
- **[Microsoft Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/)**
- **[Model Context Protocol](https://modelcontextprotocol.io/)**
- **[Azure OpenAI Service](https://learn.microsoft.com/en-us/azure/ai-services/openai/)**

---

**Built with ❤️ for platform engineers working in secure government cloud environments**

**Version**: 0.1.23  
**Last Updated**: October 29, 2025  
**Maintained by**: Micrsoft Federal CSU Platform Engineering Team

---

## 📞 Support

For issues, questions, or contributions:
- **GitHub Issues**: [Report bugs or request features](https://github.com/azurenoops/platform-engineering-copilot/issues)
- **Documentation**: [Complete documentation index](./docs/DEVELOPMENT.md)
- **Architecture**: [System design guide](./docs/ARCHITECTURE.md)
- **Docker Guide**: [Docker deployment options](./DOCKER-COMPOSE-GUIDE.md)

---

## ✨ What's New in 2.1

### MCP Server Architecture
- 🔧 **Model Context Protocol Server**: Dual-mode operation (HTTP + stdio)
- 🤖 **Multi-Agent Orchestration**: 6 specialized AI agents
- 🔌 **AI Client Integration**: GitHub Copilot and Claude Desktop support
- 📦 **Flexible Deployment**: Essentials (MCP only) and Full platform options

### Enhanced Features
- 📊 **Gap Analysis**: Automated compliance gap identification in Compliance Agent
- 💰 **Cost Overview Dashboard**: Comprehensive cost visibility in Cost Optimization Agent
- 🛡️ **NIST 800-53 Rev 5**: Complete implementation with 18 control families
- 🔐 **RMF Framework**: Full Risk Management Framework support
- 📝 **ATO Documentation**: Automated SSP, SAR, and POAM generation

### Deployment Improvements
- 🐳 **Docker Compose Configurations**: Essentials vs Full platform options
- 📖 **Enhanced Documentation**: New guides for Docker, deployment, and integration
- 🚀 **Production Ready**: Scaling, resource limits, and health checks
- **Entity Framework Core** - ORM
- **SQLite** - Development database
- **SignalR** - Real-time chat

### Frontend
- **React 18** - UI framework
- **Tailwind CSS** - Styling
- **Monaco Editor** - Code editor
- **Axios** - HTTP client

### Infrastructure
- **Terraform** - AWS/GCP provisioning
- **Bicep** - Azure provisioning
- **Kubernetes** - Container orchestration
- **Docker** - Containerization

### AI/ML
- **Model Context Protocol (MCP)** - AI tool integration
- **Natural Language Processing** - Parameter extraction
- **Intent Classification** - Workflow routing

## 📚 Advanced Topics

### Authentication & Security
- **[Quick Start Authentication](./QUICKSTART-AUTHENTICATION.md)** - Get started with Azure authentication in 30 seconds
- **[Azure Authentication Guide](./AZURE-AUTHENTICATION.md)** - Comprehensive authentication architecture and setup
- **[Authentication Cheat Sheet](./CHEATSHEET-AUTHENTICATION.md)** - Quick reference for developers
- **[Authentication Docs Summary](./AUTHENTICATION-DOCS.md)** - Navigation hub for all auth documentation

### Long-Running Operations
- **[Long-Running Tasks Architecture](./LONG-RUNNING-TASKS.md)** - Complete async job pattern design
- **[Long-Running Tasks Summary](./LONG-RUNNING-TASKS-SUMMARY.md)** - Implementation guide and API usage
- **[Long-Running Tasks Cheat Sheet](./CHEATSHEET-LONG-RUNNING-TASKS.md)** - Quick reference for async operations

### Testing & Development
- **[Natural Language Test Cases](./NATURAL-LANGUAGE-TEST-CASES.md)** - 50+ test scenarios for multi-agent system

### For Developers
- **[Generic ServiceCreation Framework](./docs/GENERIC-ServiceCreation-FRAMEWORK.md)** - Build custom ServiceCreation workflows
- **[Generator Architecture](./docs/ARCHITECTURE.md#generator-architecture)** - Create new generators
- **[Implementation Roadmap](./docs/IMPLEMENTATION-ROADMAP.md)** - Development roadmap

### For DevOps
- **[Docker Deployment](./DEPLOYMENT.md)** - Container deployment guide
- **[Networking Configuration](./docs/NETWORKING-CONFIGURATION-GUIDE.md)** - VNet/VPC setup
- **[Monitoring Setup](./docs/MONITORING-ENABLEMENT-GUIDE.md)** - Observability configuration

### For Security Engineers
- **[Zero Trust Guide](./docs/ZERO-TRUST-SECURITY-GUIDE.md)** - Zero Trust implementation
- **[Security Testing](./docs/ZERO-TRUST-TESTING-RESULTS.md)** - Security validation results

## 🤝 Contributing

Contributions welcome! Please:
1. Read the [Architecture Guide](./docs/ARCHITECTURE.md)
2. Check existing [documentation](./docs/INDEX.md)
3. Submit pull requests with tests
4. Follow existing code patterns

## � License

Copyright © 2025 Microsoft Federal

---

**Maintained by**: Micrsoft Federal CSU Platform Engineering Team
**Last Updated**: October 29, 2025  
**Documentation**: [Complete Index](./docs/INDEX.md)

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🔗 Links

- **[Azure Government Documentation](https://docs.microsoft.com/en-us/azure/azure-government/)**
- **[Model Context Protocol Specification](https://modelcontextprotocol.io/)**
- **[FedRAMP Compliance Guidelines](https://www.fedramp.gov/)**
- **[Platform Engineering Best Practices](https://platformengineering.org/)**

---

*Built with ❤️ for platform engineers working in secure government cloud environments*

## 🎯 What This Does

- **🏗️ Infrastructure**: Create Azure resources, deploy with Terraform/Bicep
- **🐳 Containers**: Deploy to Kubernetes, build Docker images  
- **🛡️ Security**: Vulnerability scanning, ATO compliance checks
- **📊 Monitoring**: Create dashboards, setup alerts
- **🚀 Applications**: Deploy apps with approval workflows

## 🗣️ Chat with AI

Use natural language in VS Code Copilot Chat:

```
@platform provision infrastructure for a web app with database
@mission-owner deploy my application to staging environment  
@platform run security scan on container "myapp:latest"
@mission-owner check if my resources are ATO compliant
```

## 🏗️ Architecture

**Dual MCP Servers:**
- **Platform Server (8080)**: Infrastructure, containers, monitoring, security
- **Mission Owner Server (8081)**: Application deployment, ATO compliance, governance

**VS Code Extension:**  
- Chat participants: `@platform`
- 20+ commands for platform engineering operations
- Multi-cloud Azure authentication + GitHub integration

---

**🎉 Ready to get started? [Read the complete documentation](DOCUMENTATION.md) for everything you need!**