# Platform Engineering Copilot

> **AI-Powered Infrastructure Provisioning & Governance Platform for Azure Government & Multi-Cloud Environments**

An enterprise-grade platform engineering solution that combines AI-powered natural language interfaces with real-time Azure resource management, compliance automation, and multi-cloud infrastructure orchestration. Built on .NET 9.0 and Microsoft Semantic Kernel, it provides intelligent infrastructure provisioning, ATO compliance scanning, cost optimization, and policy-aware deployment workflows.

---

## 🌟 Overview

The Platform Engineering Copilot transforms cloud infrastructure management by providing:

- **🤖 AI-Powered Infrastructure Provisioning**: Natural language queries to Azure Resource Manager with real-time resource creation
- **🛡️ ATO Compliance Automation**: NIST 800-53 Rev 5 compliance scanning with automated remediation recommendations
- **💰 Cost Intelligence**: Real-time cost analysis, budget tracking, and optimization recommendations
- **🔐 Policy-Aware Deployments**: Azure Policy integration with approval workflows for policy exceptions
- **📊 Real-Time Chat Interface**: SignalR-based conversational AI for infrastructure operations
- **🎯 Multi-Cloud Templates**: Generate production-ready Terraform, Bicep, and Kubernetes manifests
- **🔧 MCP Server Integration**: Model Context Protocol server for AI agent extensibility

---

## 🚀 Quick Start

### Prerequisites

- **.NET 9.0 SDK** or later
- **Docker & Docker Compose** (for containerized deployment)
- **Azure Subscription** (Azure Government or Commercial)
- **Azure CLI** (for authentication) - **Required**
- **Redis** (optional, for caching)

### 1. Clone and Build

```bash
git clone https://github.com/azurenoops/platform-engineering-copilot.git
cd platform-engineering-copilot
dotnet build
```

### 2. Configure Azure Authentication

The application uses **DefaultAzureCredential** - no hardcoded credentials needed!

```bash
# For Azure Government
az cloud set --name AzureUSGovernment
az login
export AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)

# For Azure Commercial
az cloud set --name AzureCloud
az login

# Set your default subscription (optional)
az account set --subscription "YOUR-SUBSCRIPTION-ID"
```

**📖 See [QUICKSTART-AUTHENTICATION.md](./QUICKSTART-AUTHENTICATION.md) for detailed setup**  
**📖 See [AZURE-AUTHENTICATION.md](./AZURE-AUTHENTICATION.md) for comprehensive authentication guide**

### 3. Configure Application Settings

Edit `src/Platform.Engineering.Copilot.API/appsettings.Development.json`:

```json
{
  "Gateway": {
    "Azure": {
      "UseManagedIdentity": false,
      "CloudEnvironment": "AzureGovernment",
      "Enabled": true
    },
    "AzureOpenAI": {
      "Endpoint": "https://YOUR-OPENAI-ENDPOINT.openai.azure.us/",
      "DeploymentName": "gpt-4o",
      "ApiKey": "YOUR-API-KEY",
      "UseManagedIdentity": false
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=environment_management.db"
  }
}
```

**Note:** No Azure credentials needed in configuration - Azure CLI credentials are used automatically!

### 4. Run the API

```bash
cd src/Platform.Engineering.Copilot.API
dotnet run  # http://localhost:7001
```

### 5. Try Natural Language Queries

**Using REST API**:
```bash
curl -X POST http://localhost:7001/api/chat/intelligent-query \
  -H "Content-Type: application/json" \
  -d '{
    "conversationId": "test-123",
    "message": "Create storage account data001 in rg-dr in usgovvirginia"
  }'
```

**Example Queries**:
```
"List all resource groups in my subscription"
"Create storage account data001 in resource group rg-dr"
"Run ATO compliance assessment for my subscription"
"Show me cost analysis for the last 30 days"
"Check Azure policies for storage account in usgovvirginia"
```

---

## 🎯 Key Features

### 🤖 AI-Powered Infrastructure Operations

**Real Azure Resource Management**:
- Direct integration with Azure Resource Manager APIs
- Create, list, and manage Azure resources via natural language
- Automatic resource group creation with managed tags
- Support for Storage Accounts, Virtual Networks, Key Vaults, and more

**Intelligent Intent Classification**:
- Semantic Kernel-powered query understanding
- 7 specialized plugins: Infrastructure, Compliance, Cost Management, Deployment, Environment Management, Resource Discovery, Onboarding
- Context-aware conversations with memory across sessions

### 🛡️ ATO Compliance & Governance

**NIST 800-53 Rev 5 Compliance Engine**:
- Automated scanning of 18 control families (1000+ controls)
- FedRAMP High, Moderate, and Low baseline assessments
- Real-time policy evaluation via Azure Policy Insights API
- Risk assessment with POAM generation

**Compliance Features**:
```
✅ AC (Access Control) - 25 controls
✅ AT (Awareness and Training) - 18 controls  
✅ AU (Audit and Accountability) - 16 controls
✅ CA (Assessment, Authorization, and Monitoring) - 9 controls
✅ CM (Configuration Management) - 14 controls
✅ CP (Contingency Planning) - 13 controls
✅ IA (Identification and Authentication) - 12 controls
✅ IR (Incident Response) - 10 controls
✅ MA (Maintenance) - 6 controls
✅ MP (Media Protection) - 8 controls
✅ PE (Physical and Environmental Protection) - 23 controls
✅ PL (Planning) - 11 controls
✅ PM (Program Management) - 16 controls
✅ PS (Personnel Security) - 9 controls
✅ RA (Risk Assessment) - 10 controls
✅ SA (System and Services Acquisition) - 23 controls
✅ SC (System and Communications Protection) - 51 controls
✅ SI (System and Information Integrity) - 23 controls
```

### 💰 Cost Management & Optimization

**Azure Cost Management Integration**:
- Real-time cost analysis with breakdowns by resource group, type, location, and tags
- Budget monitoring with alerts
- Cost forecasting and trend analysis
- Optimization recommendations for right-sizing and reserved instances

**Cost Query Examples**:
```
"Show cost analysis for subscription [id] for last 3 months"
"Show budget status for subscription [id]"
"Provide optimization recommendations for production resources"
```

### 🔐 Azure Policy Integration

**Policy-Aware Infrastructure**:
- Real-time policy evaluation before deployment
- Database-backed approval workflows for policy exceptions
- Severity-based decisions (Critical, High, Medium, Low)
- 5-minute intelligent caching for performance optimization

**Policy Workflow**:
1. **Pre-Deployment Validation**: Evaluate proposed resources against active policies
2. **Approval Workflows**: Policy violations trigger approval requests (stored in database)
3. **Exception Management**: Time-bounded exceptions with justifications and mitigations
4. **Audit Trail**: Complete history of approvals, rejections, and changes

---

## 🏗️ Architecture

### System Components

```
┌─────────────────────────────────────────────────────────┐
│                    CLIENT LAYER                         │
├─────────────────────────────────────────────────────────┤
│  • Admin Console (React) - Port 3001                    │
│  • Chat App (React + SignalR) - Port 3000               │
│  • MCP Clients (AI Agents)                              │
└─────────────────────────────────────────────────────────┘
                       ↓ REST API / SignalR
┌─────────────────────────────────────────────────────────┐
│                    API LAYER                            │
├─────────────────────────────────────────────────────────┤
│  • Platform.Engineering.Copilot.API (Port 7001)         │
│  • Platform.Engineering.Copilot.Admin.API (Port 7002)   │
│  • Platform.Engineering.Copilot.Mcp (Console)           │
└─────────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────────┐
│                 BUSINESS LOGIC LAYER                    │
├─────────────────────────────────────────────────────────┤
│  Platform.Engineering.Copilot.Core                      │
│  ├── Semantic Kernel Plugins (7)                        │
│  │   ├── InfrastructurePlugin (AI-powered)              │
│  │   ├── CompliancePlugin (AI-powered)                  │
│  │   ├── CostManagementPlugin                           │
│  │   ├── ResourceDiscoveryPlugin                        │
│  │   ├── EnvironmentManagementPlugin                    │
│  │   ├── DeploymentPlugin                               │
│  │   └── OnboardingPlugin                               │
│  └── Domain Services                                    │
│      ├── InfrastructureProvisioningService (Real API)   │
│      ├── AtoComplianceEngine (NIST 800-53)              │
│      ├── AzureCostManagementService                     │
│      ├── AzurePolicyEngine (Policy Insights API)        │
│      ├── IntelligentChatService (Semantic Kernel)       │
│      └── [40+ services]                                 │
└─────────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────────┐
│              DOMAIN EXTENSIONS                          │
├─────────────────────────────────────────────────────────┤
│  • Platform.Engineering.Copilot.Governance              │
│  • Platform.Engineering.Copilot.DocumentProcessing      │
└─────────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────────┐
│                 DATA LAYER                              │
├─────────────────────────────────────────────────────────┤
│  Platform.Engineering.Copilot.Data                      │
│  • Entity Framework Core 9.0                            │
│  • PlatformEngineeringCopilotContext (20+ DbSets)            │
│  • Entities: OnboardingRequest, EnvironmentTemplate,    │
│    ApprovalWorkflow, ComplianceAssessment               │
│  • Supports: SQL Server, SQLite, In-Memory              │
└─────────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────────┐
│              EXTERNAL SERVICES                          │
├─────────────────────────────────────────────────────────┤
│  • Azure Resource Manager (management.usgovcloudapi.net)│
│  • Azure Policy Insights API                            │
│  • Azure Cost Management API                            │
│  • Azure OpenAI (GPT-4o)                                │
│  • Azure Storage, Key Vault, SQL Database               │
└─────────────────────────────────────────────────────────┘
```

### Dependency Hierarchy

```
Level 0 (Foundation):
└── Data (NO DEPENDENCIES - Isolated)

Level 1 (Core Business Logic):
└── Core ──→ Data

Level 2 (Domain Extensions):
├── Governance ──→ Core ──→ Data
└── DocumentProcessing ──→ Core, Governance

Level 3 (Execution Layer):
└── Mcp ──→ Core

Level 4 (API Layer):
├── API ──→ Core, Data, Governance, DocumentProcessing
├── Chat.App ──→ Core, Data, Governance
└── Admin.API ──→ Core, Data
```

---

## 📚 Documentation

### Getting Started
- **[DEVELOPMENT.md](./DEVELOPMENT.md)** - Development setup, configuration, and local development guide (Updated: Jan 17, 2025)
- **[DEPLOYMENT.md](./DEPLOYMENT.md)** - Docker, Kubernetes, and cloud deployment instructions (Updated: Jan 17, 2025)
- **[ARCHITECTURE.md](./ARCHITECTURE.md)** - Detailed system architecture, components, and data flows (Updated: Jan 17, 2025)
- **[PROMPT-GUIDE.md](./PROMPT-GUIDE.md)** - Comprehensive guide to writing effective natural language prompts (Updated: Jan 17, 2025)

### Advanced Topics
- **[M365 Copilot Integration](./docs/M365-COPILOT-INTEGRATION.md)** - Deploy as Microsoft 365 Copilot declarative agent
- **[Documentation Update Summary](./DOCUMENTATION-UPDATE-SUMMARY.md)** - Recent documentation changes and verification checklist

---

## 🛠️ Technology Stack

### Backend
| Technology | Version | Purpose |
|------------|---------|---------|
| **.NET** | 9.0 | Primary framework |
| **Microsoft Semantic Kernel** | 1.26.0 | AI orchestration |
| **Entity Framework Core** | 9.0.0 | Data access |
| **Azure SDK** | 1.48.0+ | Azure resource management |
| **SignalR** | 9.0.0 | Real-time communication |

### AI & ML
| Technology | Purpose |
|------------|---------|
| **Azure OpenAI (GPT-4o)** | Natural language understanding |
| **Semantic Kernel** | AI plugin orchestration |
| **Intent Classification** | Query routing to specialized plugins |
| **Context Management** | Conversation memory across sessions |

### Azure Integration
| Service | Purpose |
|---------|---------|
| **Azure Resource Manager** | Infrastructure provisioning |
| **Azure Policy Insights API** | Policy evaluation |
| **Azure Cost Management API** | Cost analysis |
| **Azure Storage** | Blob storage, file storage |
| **Azure Key Vault** | Secrets management |
| **Azure SQL Database** | Production data storage |

### Infrastructure
| Technology | Purpose |
|------------|---------|
| **Terraform** | Multi-cloud IaC |
| **Bicep** | Azure-native IaC |
| **Kubernetes** | Container orchestration |
| **Docker** | Containerization |

---

## 🎯 Use Cases

### 1. Infrastructure Provisioning
```
User: "Create storage account data001 in resource group rg-dr in usgovvirginia"

System:
✅ Validates subscription ID and authentication
✅ Checks if resource group exists (creates if needed)
✅ Creates storage account with security settings:
   - HTTPS-only traffic
   - TLS 1.2 minimum
   - Standard_LRS SKU
✅ Returns resource ID and provisioning status
```

### 2. ATO Compliance Scanning
```
User: "Run ATO compliance assessment for my subscription"

System:
✅ Scans all 18 NIST 800-53 control families
✅ Evaluates 1000+ controls against Azure resources
✅ Generates compliance report with:
   - Overall compliance score (e.g., 60.85%)
   - Critical/High/Medium/Low findings
   - Remediation recommendations
   - Risk assessment
✅ Stores assessment in database for audit trail
```

### 3. Cost Analysis
```
User: "Show cost analysis for subscription [id] for last 3 months"

System:
✅ Queries Azure Cost Management API
✅ Retrieves cost data with breakdowns:
   - By resource group
   - By resource type
   - By location
   - By tags
✅ Calculates trends and forecasts
✅ Provides optimization recommendations
```

### 4. Policy Evaluation
```
User: "Check Azure policies for storage account in usgovvirginia"

System:
✅ Calls Azure Policy Insights API
✅ Evaluates active policies against proposed resource
✅ Returns policy violations with:
   - Severity (Critical/High/Medium/Low)
   - Policy definition
   - Remediation guidance
✅ Triggers approval workflow if violations found
```

---

## 🔐 Security Features

### Authentication & Authorization
- **Azure AD Integration**: Managed Identity and Service Principal support
- **RBAC**: Role-based access control for API endpoints
- **Key Vault Integration**: Secure secrets management

### Compliance & Governance
- **NIST 800-53 Rev 5**: Complete control family implementation
- **FedRAMP Baselines**: High, Moderate, and Low compliance levels
- **Azure Policy**: Real-time policy evaluation and enforcement
- **Audit Logging**: Complete audit trail in database

### Network Security
- **Private Endpoints**: Support for Azure Private Link
- **Zero Trust**: Network segmentation and micro-segmentation
- **Encryption**: Data at rest and in transit

---

## 📊 Key Capabilities

### Real-Time Infrastructure Operations
✅ List resource groups across subscriptions  
✅ Create storage accounts with security settings  
✅ Provision virtual networks with subnets  
✅ Deploy Key Vaults with HSM support  
✅ Manage blob containers  
✅ Auto-create resource groups with tags  

### ATO Compliance Automation
✅ NIST 800-53 Rev 5 scanning (18 control families)  
✅ FedRAMP High/Moderate/Low assessments  
✅ Remediation plan generation  
✅ POAM (Plan of Action & Milestones) creation  
✅ eMASS package generation  
✅ Risk assessment and scoring  

### Cost Intelligence
✅ Real-time cost analysis with breakdowns  
✅ Budget tracking and alerts  
✅ Cost forecasting and trend analysis  
✅ Optimization recommendations  
✅ Right-sizing suggestions  
✅ Reserved instance analysis  

### Policy Management
✅ Azure Policy evaluation (real-time)  
✅ Approval workflows for policy exceptions  
✅ Database-backed workflow persistence  
✅ Severity-based decision automation  
✅ 5-minute intelligent caching  

---

## 🚀 Deployment Options

### Local Development
```bash
dotnet run --project src/Platform.Engineering.Copilot.API
```

### Docker Compose

**Multiple configurations available for different deployment scenarios:**

```bash
# MCP Server only (essentials)
docker-compose -f docker-compose.essentials.yml up -d

# All services (default)
docker-compose up -d

# Development with hot reload
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up -d

# Production with scaling
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

**📖 See [DOCKER-COMPOSE-GUIDE.md](./DOCKER-COMPOSE-GUIDE.md) for configuration details**  
**📖 See [DOCKER.md](./DOCKER.md) for comprehensive Docker documentation**

### Kubernetes
```bash
kubectl apply -f infra/k8s/
```

### Azure App Service
See [DEPLOYMENT.md](./DEPLOYMENT.md) for Azure deployment instructions.

---

## 🤝 Contributing

Contributions welcome! Please:
1. Read the [ARCHITECTURE.md](./ARCHITECTURE.md) guide
2. Check existing [documentation](./DEVELOPMENT.md)
3. Submit pull requests with tests
4. Follow existing code patterns

---

## 📄 License

Copyright © 2025 Platform Engineering Team

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🔗 Resources

- **[Azure Government Documentation](https://docs.microsoft.com/en-us/azure/azure-government/)**
- **[NIST 800-53 Rev 5](https://csrc.nist.gov/publications/detail/sp/800-53/rev-5/final)**
- **[FedRAMP Compliance](https://www.fedramp.gov/)**
- **[Microsoft Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/)**
- **[Model Context Protocol](https://modelcontextprotocol.io/)**

---

**Built with ❤️ for platform engineers working in secure government cloud environments**

**Version**: 2.0  
**Last Updated**: January 17, 2025  
**Maintained by**: Platform Engineering Team

---

## 📞 Support

For issues, questions, or contributions:
- **GitHub Issues**: [Report bugs or request features](https://github.com/azurenoops/platform-engineering-copilot/issues)
- **Documentation**: [Complete documentation index](./DEVELOPMENT.md)
- **Architecture**: [System design guide](./ARCHITECTURE.md)
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