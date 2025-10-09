# Platform Engineering Copilot

A comprehensive platform engineering solution that automates infrastructure provisioning, onboarding workflows, and service deployment across Azure, AWS, and GCP. Features AI-powered onboarding, multi-cloud template generation, and zero-trust security.

## 🌟 Overview

The Platform Engineering Copilot transforms platform engineering by providing:

- **🤖 AI-Powered Onboarding**: Natural language onboarding via chat with military rank intelligence
- **🏗️ Multi-Cloud Templates**: Generate 35+ files per service (Terraform, Bicep, K8s, CI/CD)
- **☁️ Cloud Support**: Azure, AWS, GCP with 15+ compute platforms
- **🔒 Zero Trust Security**: Built-in network policies, RBAC, service mesh integration
- **📊 Admin Console**: React-based UI for template management and approvals
- **🔧 MCP Integration**: Model Context Protocol for AI-driven operations

## 📚 Documentation

**[📖 Complete Documentation Index](./docs/INDEX.md)** - Start here for organized access to all docs

### Quick Links
- **[Architecture](./docs/ARCHITECTURE.md)** - System design, components, and data flows
- **[Mission Owner Guide](./docs/MISSION-OWNER-USER-GUIDE.md)** - How to onboard services
- **[Admin Guide](./docs/NNWC-ADMIN-GUIDE.md)** - Admin console usage
- **[Onboarding Framework](./docs/GENERIC-ONBOARDING-FRAMEWORK.md)** - Developer guide
- **[Security Guide](./docs/ZERO-TRUST-SECURITY-GUIDE.md)** - Zero Trust implementation
- **[Deployment Guide](./docs/ADMIN-SYSTEM-DEPLOYMENT.md)** - Production deployment

## 🚀 Quick Start

### Prerequisites

- **.NET 8 SDK** or later
- **Docker & Docker Compose** (for containerized deployment)
- **Node.js 18+** (for admin console frontend)
- **Azure/AWS/GCP Subscription** (optional for provisioning)

### 1. Clone and Build

```bash
git clone https://github.com/jrspinella/platform-mcp-Platform.Engineering.Copilot.git
cd platform-mcp-supervisor
dotnet build
```

### 2. Run Locally with Docker

```bash
# Start all services (API, Chat, Admin Console, Database, Redis)
docker-compose -f docker-compose.dev.yml up -d

# Access services:
# - Admin Console: http://localhost:3000
# - Chat App: http://localhost:3001  
# - API: http://localhost:7001
```

### 3. Or Run Components Individually

```bash
# Terminal 1: Start API
cd src/Platform.Engineering.Copilot.API
dotnet run  # http://localhost:7001

# Terminal 2: Start Admin Console
cd admin-client
npm install && npm run dev  # http://localhost:3000
```

### 4. Try the Onboarding Chat

Open chat at `http://localhost:3001` and say:

```
I need to onboard a new mission for NAVWAR
```

The AI will guide you through the onboarding process, intelligently extracting:
- Mission details (name, description, mission owner)
- Rank and service branch (e.g., "CDR" → "Commander (O-5), Navy")
- Required services (AKS, SQL Server, Storage)
- Network requirements (VNet CIDR, subnets)

## 🎯 Key Features

### AI-Powered Onboarding
- **Natural Language Processing**: Extract fields from conversational input
- **Military Rank Intelligence**: Normalize 80+ rank variants with service branch
- **Multi-Step Workflows**: Guided onboarding with validation
- **Cross-Session Recovery**: Resume onboarding from any chat session

### Template Generation (35+ Files)
- **Infrastructure**: Terraform modules (10 files), Bicep modules (8 files), K8s manifests (8 files)
- **Application**: Language-specific source code for .NET, Node.js, Python, Java, Go, Rust
- **CI/CD**: GitHub Actions workflows for build, deploy, environments
- **DevOps**: Dockerfiles (multi-stage), README, deployment guides

### Multi-Cloud Support
| Platform | Provider | Terraform | Bicep | K8s | Status |
|----------|----------|-----------|-------|-----|--------|
| **EKS** | AWS | ✅ | ❌ | ✅ | Production |
| **GKE** | GCP | ✅ | ❌ | ✅ | Production |
| **AKS** | Azure | ✅ | ✅ | ✅ | Production |
| **ECS** | AWS | ✅ | ❌ | N/A | Production |
| **Lambda** | AWS | ✅ | ❌ | N/A | Production |
| **Cloud Run** | GCP | ✅ | ❌ | N/A | Production |
| **Container Apps** | Azure | ❌ | ✅ | N/A | Production |

### Admin Console Features
- ✅ Template browsing with search/filter
- ✅ Template CRUD operations
- ✅ File viewer with syntax highlighting
- ✅ Onboarding approval workflows
- ✅ Network configuration (VNet/VPC setup)
- ✅ Deployment progress tracking

## 🏗️ Architecture

See **[docs/ARCHITECTURE.md](./docs/ARCHITECTURE.md)** for complete system design.

### High-Level Overview

```
┌─────────────────────────────────────────────────────┐
│  Admin Console (React) + Chat App (React)           │
└─────────────────────────────────────────────────────┘
                       ↓ REST API
┌─────────────────────────────────────────────────────┐
│  API Gateway (.NET 8)                               │
│  • TemplateAdminController                          │
│  • OnboardingController                             │
│  • ChatController                                   │
└─────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│  Service Layer                                      │
│  • DynamicTemplateGenerator (35 files/template)     │
│  • UnifiedInfrastructureOrchestrator (multi-cloud)  │
│  • FlankspeedOnboardingService (workflows)          │
│  • IntelligentChatService (AI routing)              │
└─────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│  Generators (Bicep, Terraform, K8s, CI/CD, Docker)  │
└─────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│  Cloud Providers (Azure, AWS, GCP)                  │
└─────────────────────────────────────────────────────┘
```
## 🛠️ Technology Stack

### Backend
- **.NET 8** - API and core services
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

### For Developers
- **[Generic Onboarding Framework](./docs/GENERIC-ONBOARDING-FRAMEWORK.md)** - Build custom onboarding workflows
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

Copyright © 2025 Platform Engineering Team

---

**Maintained by**: Platform Engineering Team  
**Last Updated**: October 6, 2025  
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
- Chat participants: `@platform` and `@mission-owner`
- 20+ commands for platform engineering operations
- Multi-cloud Azure authentication + GitHub integration

---

**🎉 Ready to get started? [Read the complete documentation](DOCUMENTATION.md) for everything you need!**