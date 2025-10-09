# Platform Engineering Copilot - Comprehensive Prompt Guide

> **Master the Art of AI-Powered Infrastructure Provisioning with Natural Language**

---

## 📋 Table of Contents

1. [Introduction](#introduction)
2. [Prompt Fundamentals](#prompt-fundamentals)
3. [Chat Interface Prompts](#chat-interface-prompts)
4. [VS Code Extension Prompts](#vs-code-extension-prompts)
5. [Admin Console Operations](#admin-console-operations)
6. [Advanced Techniques](#advanced-techniques)
7. [Best Practices](#best-practices)
8. [Troubleshooting Prompts](#troubleshooting-prompts)

---

## 🎯 Introduction

The Platform Engineering Copilot uses natural language processing to understand your infrastructure needs and automatically generate production-ready templates. This guide teaches you how to write effective prompts to get the best results.

### Understanding the AI System

The system uses three levels of intelligence:

1. **Intent Classification**: Determines what you want to do (provision, deploy, monitor, etc.)
2. **Parameter Extraction**: Extracts structured data from your conversational input
3. **Context Awareness**: Remembers previous messages and maintains conversation state

### Three Primary Interfaces

1. **Chat App** (`http://localhost:3001`): Natural language onboarding and support
2. **VS Code Extension**: `@platform` chat participant for infrastructure operations
3. **Admin Console** (`http://localhost:3000`): Template management and approvals

---

## 🎓 Prompt Fundamentals

### The Anatomy of a Good Prompt

**Structure**:
```
[CONTEXT] + [ACTION] + [REQUIREMENTS] + [CONSTRAINTS]
```

**Example**:
```
Context: "I'm a Navy Commander working on a classified mission"
Action: "I need to deploy a web application"
Requirements: "to Azure Government with SQL database and blob storage"
Constraints: "must meet FedRAMP High compliance"

Complete Prompt:
"I'm a Navy Commander working on a classified mission. I need to deploy 
a web application to Azure Government with SQL database and blob storage. 
It must meet FedRAMP High compliance requirements."
```

### Progressive Disclosure

Start simple, add details as needed:

**Level 1 - Basic Request**:
```
"I need to deploy a web application"
```

**Level 2 - Add Cloud Context**:
```
"I need to deploy a web application to Azure"
```

**Level 3 - Add Security Requirements**:
```
"I need to deploy a web application to Azure Government with FedRAMP compliance"
```

**Level 4 - Complete Specification**:
```
"I need to deploy a .NET web application to Azure Government using AKS 
with Azure SQL database, Redis cache, and blob storage. Must meet 
FedRAMP High compliance with zero-trust networking."
```

### Key Information to Include

| Category | What to Include | Example |
|----------|----------------|---------|
| **Identity** | Rank, service branch, organization | "CDR Johnson, Navy, NAVWAR" |
| **Mission** | Mission name, purpose, classification | "Secure Comms Platform, CUI" |
| **Cloud** | Provider, environment, region | "Azure Government, US Gov Virginia" |
| **Services** | Compute, database, storage, networking | "AKS, SQL Server, Blob Storage, VNet" |
| **Compliance** | Framework, level | "FedRAMP High, NIST 800-53" |
| **Scale** | Size, traffic, users | "10 nodes, 50K users, HA required" |

---

## 💬 Chat Interface Prompts

### 1. Service Onboarding Workflows

#### Basic Onboarding

**Simple Onboarding**:
```
"I need to onboard a new mission for NAVWAR"
```

**System Response**:
```
Great! I'll help you onboard a new mission. Let me gather some information:

1. What's the mission name?
2. What's your rank and service branch?
3. What type of application are you deploying?
4. Which cloud environment do you need (Commercial or Government)?
```

**Complete Onboarding in One Prompt**:
```
"I need to onboard a new mission called 'Tactical Edge Platform' for NAVWAR. 
I'm Commander Sarah Johnson from the Navy. We need to deploy a microservices 
architecture with AKS cluster, Azure SQL Database, Redis cache, and blob storage.
My email is sarah.johnson@navy.mil. Classification is CUI. We expect about 
5000 concurrent users and need FedRAMP High compliance. Default VNet is fine."
```

**System Response** (Interactive Multi-turn):
```
System: "I've started your Flankspeed onboarding for 'Tactical Edge Platform'.

📝 Information captured:
✅ Mission: Tactical Edge Platform
✅ Organization: NAVWAR  
✅ Rank: Commander (O-5), Navy
✅ Email: sarah.johnson@navy.mil
✅ Classification: CUI
✅ Services: AKS, Azure SQL, Redis, Blob Storage
✅ Expected users: 5000
✅ VNet: 10.100.0.0/16 (default)

I still need:
- Which Azure region? (Commercial or Government?)
- What's your command/unit?
- Any specific compliance frameworks beyond FedRAMP High?

Please provide these details to complete your onboarding request."
```

**Note**: The system uses **interactive conversation** to gather all required fields. 
It doesn't automatically infer cost estimates or generate infrastructure until all 
information is collected and the request is approved.

#### Government Cloud Onboarding

**Classified Mission**:
```
"I'm deploying a classified intelligence application to Azure Government. 
Mission name is 'Operation Oversight'. I'm a Lieutenant Commander in the 
Navy, assigned to NSA. Requirements:
- Classification: SECRET
- Compliance: FedRAMP High + DoD IL4
- Services: Kubernetes cluster, SQL Server with Always Encrypted, 
  Key Vault with HSM, isolated VNet with no internet access
- Data sovereignty: All resources must stay in US Gov regions
- Multi-region: Primary in Gov Virginia, DR in Gov Texas
- Estimated monthly cost: <$10,000"
```

**Multi-Cloud Deployment**:
```
"I need to deploy the same microservice to both Azure Commercial (dev/staging) 
and Azure Government (production). Service name is 'payment-processor'. 
Requirements:
- Dev: Azure AKS in East US 2, standard tier
- Staging: Azure AKS in West US 2, standard tier
- Production: Azure Government AKS in Gov Virginia, FedRAMP High
- All environments need: PostgreSQL, Redis, Service Bus
- CI/CD: GitHub Actions with approval gates for production
- Cost constraint: Total <$5000/month across all environments"
```

### 2. Military Rank & Identity Prompts

The system understands 80+ military rank variations. You can use any format:

**Rank Variations** (all understood):
```
✅ "I'm CDR Johnson"
✅ "I'm Commander Johnson"
✅ "I'm a Commander (O-5)"
✅ "I'm LCDR Smith"
✅ "I'm Lieutenant Commander"
✅ "I'm Maj Williams" (Air Force)
✅ "I'm MAJ Williams" (Army)
✅ "I'm Captain Johnson" (when service branch specified, resolves ambiguity)
✅ "I'm a Navy O-5"
✅ "I'm an E-7 in the Air Force"
```

**Service Branch Specifications**:
```
Navy: "CDR Johnson, Navy" or "I'm with NAVWAR"
Army: "MAJ Smith, USA" or "I'm from Army Cyber Command"
Air Force: "Maj Williams, USAF" or "I'm stationed at Vandenberg SFB"
Marines: "Capt Brown, USMC" or "I'm with Marine Corps Forces Cyberspace"
Space Force: "Capt Davis, USSF" or "I'm with Space Systems Command"
Coast Guard: "LCDR Taylor, USCG" or "I'm with Coast Guard Cyber Command"
```

### 3. Onboarding Request Specifications

The chat interface is designed for **onboarding workflows**, not direct infrastructure deployment. When you describe infrastructure requirements, the system captures them as part of an onboarding request that goes through approval before template generation.

**What Works - Describing Infrastructure Needs During Onboarding**:
```
"For my mission, we need:
- Kubernetes cluster (AKS) with about 5 nodes
- Azure SQL Database for persistence
- Redis cache for sessions
- Blob storage for documents
- We expect 5000 users
- Classification is CUI
- Need FedRAMP High compliance"
```

**System Response**:
```
"I've captured your infrastructure requirements:
✅ Compute: AKS cluster
✅ Database: Azure SQL
✅ Cache: Redis
✅ Storage: Blob Storage
✅ Scale: 5000 users
✅ Compliance: FedRAMP High

After your request is approved, we'll generate infrastructure templates 
with these services configured for FedRAMP compliance."
```

**What Doesn't Work - Direct Deployment Commands**:
```
❌ "Deploy a Kubernetes cluster with 5 nodes"
❌ "Create an AKS cluster named mission-app-cluster"
❌ "Provision Azure SQL Database with 8 vCores"
```

**Why**: The chat interface is for **onboarding conversations**, not direct Azure API calls. Infrastructure provisioning happens **after** approval through generated templates.

**For Direct Infrastructure Operations**: Use the **Admin Console** or **deployment orchestration** after templates are generated and approved.

### 4. Infrastructure Requirements (For Onboarding)

When onboarding a mission, you can describe your infrastructure needs. The system captures these as **requirements**, not immediate deployments.

#### Compute Requirements

**Describing Container Platform Needs**:
```
"We need a container platform for our microservices:
- Kubernetes (AKS preferred)
- Start with 5 nodes, able to scale up to 10
- Need network policies for security
- Must integrate with Azure AD for authentication"
```

**Describing Web App Needs**:
```
"We're deploying a .NET web application:
- Need App Service with auto-scaling
- 2-5 instances based on load
- Must support deployment slots (dev, staging, prod)
- Need Application Insights for monitoring"
```

#### Database Requirements

**Describing Database Needs**:
```
"For data storage, we need:
- SQL Server with high availability
- Geo-replication for disaster recovery
- Encrypted at rest and in transit
- Must support Always Encrypted for PII data"
```

**Describing NoSQL Needs**:
```
"We need a NoSQL database:
- Cosmos DB for global distribution
- Multi-region replication
- Auto-scaling throughput
- Session consistency is acceptable"
```

#### Storage Requirements

**Describing Storage Needs**:
```
"For file storage:
- Blob storage for documents and media
- Hot tier for active data, cool tier for archives
- Geo-redundant replication
- Private endpoints only, no public access
- Lifecycle policies to move old data to archive"
```

**Note**: These descriptions are captured during onboarding. After approval, the platform generates appropriate infrastructure templates based on your requirements.

#### Networking

**Virtual Network Design**:
```
"Design VNet architecture for 3-tier application:
- VNet CIDR: 10.100.0.0/16 (65,536 IPs)
- Subnets:
  * Frontend subnet: 10.100.1.0/24 (Application Gateway)
  * Application subnet: 10.100.10.0/23 (AKS nodes, 512 IPs)
  * Data subnet: 10.100.20.0/24 (SQL, Redis, private endpoints)
  * Management subnet: 10.100.100.0/24 (Bastion, VPN Gateway)
- NSG rules:
  * Frontend: Allow 443 from internet, deny all inbound
  * Application: Allow 443 from frontend, allow 1433 to data subnet
  * Data: Allow from application subnet only, deny all internet
- Route table: Force tunnel all traffic through Azure Firewall
- DNS: Azure Private DNS zones for privatelink resources
- Peering: Hub VNet for shared services (firewall, DNS, monitoring)"
```

**Zero-Trust Networking**:
```
"Implement zero-trust network for classified application:
- No public IP addresses on any resource
- All ingress through Azure Application Gateway with WAF
- All egress through Azure Firewall with FQDN filtering
- Network segmentation: Micro-segmentation with NSGs per service
- Service-to-service: mTLS with service mesh (Istio)
- Identity-based access: Azure AD workload identity for pods
- Private endpoints: For all Azure PaaS services (SQL, Storage, Key Vault)
- Monitoring: NSG flow logs, firewall logs to Log Analytics
- Compliance: NIST 800-207 Zero Trust Architecture"
```

### 4. Compliance & Security Prompts

#### Compliance Scanning

**Basic Compliance Check**:
```
"Check FedRAMP compliance for my production environment"
```

**Detailed Compliance Scan**:
```
"Run comprehensive compliance scan for resource group 'mission-prod-rg' with:
- Framework: FedRAMP High (325 controls)
- Include: All resources (VMs, storage, networking, databases)
- Severity: Report all findings (critical, high, medium, low)
- Output: Detailed report with remediation steps
- Auto-fix: Where possible, automatically remediate low-risk issues
- Schedule: Run weekly, alert on new critical findings
- Evidence: Collect artifacts for ATO package"
```

**Multi-Framework Compliance**:
```
"Validate compliance against multiple frameworks:
- FedRAMP High (for government authorization)
- NIST 800-53 Rev 5 (security controls)
- ISO 27001 (information security management)
- SOC 2 Type II (trust service criteria)
- Scope: Entire Azure Government subscription
- Priority: Identify gaps blocking ATO approval
- Timeline: Need ATO within 60 days, prioritize critical gaps"
```

#### Security Hardening

**Security Baseline**:
```
"Apply security hardening to all resources in subscription:
- Encryption: Encrypt all data at rest with customer-managed keys
- Networking: Disable public access, enable private endpoints
- Authentication: Enforce Azure AD, disable local auth
- MFA: Require MFA for all admin access
- RBAC: Least privilege, no Owner role assignments to users
- Logging: Enable diagnostic settings on all resources
- Monitoring: Azure Defender for Cloud (all plans)
- Secrets: No connection strings in app config, use Key Vault references
- Certificates: Managed certificates with auto-renewal
- Vulnerabilities: Enable Defender for containers, SQL, storage"
```

**Incident Response**:
```
"Set up security incident response:
- SIEM: Microsoft Sentinel workspace
- Data sources: Azure AD logs, activity logs, NSG flow logs, WAF logs
- Analytics rules: Detect suspicious logins, privilege escalation, 
  data exfiltration, crypto-mining, lateral movement
- Automation: Auto-block suspicious IPs, disable compromised accounts, 
  isolate infected VMs
- Notifications: Email security team, create PagerDuty incident
- Playbooks: Auto-response for common incident types
- Retention: 2 years (compliance requirement)"
```

### 5. Cost Optimization Prompts

**Cost Analysis**:
```
"Analyze Azure costs for last 3 months:
- Subscription: Production subscription or SubnscriptionId
- Breakdown: By resource group, resource type, location, tags
- Trends: Show spending trends and anomalies
- Forecasting: Predict next month's costs
- Recommendations: Identify cost optimization opportunities
- Budget alerts: Notify if spending >80% of monthly budget ($50K)"
```

**Optimization Recommendations**:
```
"Provide cost optimization recommendations for production environment:
- Right-sizing: Identify oversized VMs and databases
- Reserved instances: Analyze usage for RI/savings plan opportunities
- Unused resources: Find idle resources (stopped VMs, unattached disks, 
  orphaned NICs, old snapshots)
- Storage tiering: Move infrequently accessed data to cool/archive
- Auto-shutdown: Identify non-production resources for scheduled shutdown
- Licensing: Optimize SQL licensing with Azure Hybrid Benefit
- Expected savings: Target 30-40% cost reduction
- Implementation: Prioritize quick wins, then long-term optimizations"
```

---

## 🔧 VS Code Extension Prompts

### Using the @platform Chat Participant

The VS Code extension provides the `@platform` chat participant for infrastructure operations directly in your IDE.

### 1. Infrastructure Provisioning

**Quick Resource Creation**:
```
@platform create a storage account named "myappstorage" in resource group "test-rg"
```

**Complex Multi-Resource Deployment**:
```
@platform provision complete infrastructure for microservice "order-processor":
- Resource group: orders-prod-rg in East US 2
- AKS cluster: 5 nodes, Standard_D4s_v3
- Azure SQL: Business Critical, 4 vCores, geo-replication
- Redis Cache: Premium P1, 6GB
- Storage: Premium blob storage with lifecycle policies
- Service Bus: Premium tier with topics for event-driven architecture
- Application Gateway: WAF v2 with custom domain
- Key Vault: Premium with HSM for sensitive keys
- Generate: Terraform templates, Kubernetes manifests, CI/CD pipelines
```

### 2. Template Generation

**Generate Bicep Template**:
```
@platform generate Bicep template for serverless architecture:
- Container Apps environment with 3 microservices
- Cosmos DB with SQL API
- Service Bus for async messaging
- Application Insights for monitoring
- Managed Identity for all services
- Include: Variables file, parameters, outputs
```

**Generate Terraform Template**:
```
@platform create Terraform module for AWS EKS:
- Cluster version: 1.27
- Node groups: 2 (system nodes and application nodes)
- Networking: VPC with public and private subnets
- Add-ons: AWS Load Balancer Controller, EBS CSI driver, Cluster Autoscaler
- Security: IRSA (IAM Roles for Service Accounts)
- Include: Backend config for S3, variables.tf, outputs.tf
```

### 3. Security Scanning

**Container Security Scan**:
```
@platform scan container image "myregistry.azurecr.io/webapp:v2.1.0" for:
- Vulnerabilities: CVEs in base image and dependencies
- Secrets: Hardcoded passwords, API keys, tokens
- Best practices: Dockerfile optimization, non-root user, minimal layers
- Compliance: CIS Docker Benchmark
- Tools: Trivy, Grype, Dockle
- Output: Detailed report with severity levels and remediation steps
```

**Code Security Scan**:
```
@platform run security scan on current workspace:
- SAST: Static analysis for code vulnerabilities (SQL injection, XSS, etc.)
- Dependency scan: Known vulnerabilities in NuGet/npm packages
- Secret detection: Scan for accidentally committed secrets
- License compliance: Check for GPL or other restrictive licenses
- Output: Create GitHub issue for each critical/high vulnerability
```

### 4. Deployment Operations

**Deploy to Kubernetes**:
```
@platform deploy application "payment-api" version v2.1.0 to production:
- Cluster: production-aks-eastus2
- Namespace: payments
- Strategy: Rolling update with 25% max surge, 0% max unavailable
- Health checks: /health (liveness), /ready (readiness)
- Resources: 500m CPU, 1Gi memory (requests), 1000m CPU, 2Gi memory (limits)
- Replicas: 5 (HPA: min 5, max 20, target 70% CPU)
- ConfigMap: payment-config from Key Vault
- Secrets: Database connection string, Stripe API key
- Monitoring: Send deployment event to Application Insights
```

**Blue-Green Deployment**:
```
@platform execute blue-green deployment for "customer-portal":
- Current version (green): v1.5.2 (receiving 100% traffic)
- New version (blue): v1.6.0 (deploy but no traffic)
- Validation: Run smoke tests, check error rate <1%
- Traffic shift: If validation passes, shift 10% → 50% → 100% over 30 minutes
- Rollback triggers: Error rate >2%, response time >500ms p95, or manual command
- Keep green: Maintain for 1 hour after 100% cutover, then tear down
```

### 5. Monitoring & Observability

**Create Dashboard**:
```
@platform create monitoring dashboard for "e-commerce-platform":
- Metrics:
  * Application: Request rate, error rate, response time (p50, p95, p99)
  * Infrastructure: CPU, memory, disk, network per service
  * Database: DTU utilization, query performance, deadlocks
  * Business: Orders/minute, revenue/hour, cart abandonment rate
- Time range: Last 24 hours with auto-refresh every 30 seconds
- Alerts: Visual indicators when metrics exceed thresholds
- Drill-down: Click to view logs and traces for specific time range
- Share: Generate public link for stakeholders
```

**Set Up Alerting**:
```
@platform configure alerts for production environment:
- Critical (PagerDuty):
  * Application error rate >5% for 5 minutes
  * Database DTU >90% for 10 minutes
  * Any pod in CrashLoopBackOff state
  * SSL certificate expires in <7 days
- Warning (Email):
  * Response time p95 >1000ms for 15 minutes
  * CPU >80% for 20 minutes
  * Disk usage >85%
- Informational (Slack):
  * Deployment started/completed
  * Auto-scaling events
  * Certificate rotated
```

### 6. Documentation Generation

**Generate README**:
```
@platform create comprehensive README for current repository:
- Project overview: Parse csproj/package.json for description
- Architecture: Detect services and generate architecture diagram
- Prerequisites: .NET version, Node.js, Docker, Azure resources
- Local development: Step-by-step setup instructions
- Environment variables: List all required env vars with examples
- Deployment: Azure deployment guide with CLI commands
- Testing: How to run unit tests, integration tests
- Troubleshooting: Common issues and solutions
- Contributing: How to contribute (if CONTRIBUTING.md exists)
```

---

## 📊 Admin Console Operations

### Template Management

**Browse Templates**:
```
Filter: Cloud=Azure, Service=Kubernetes, Compliance=FedRAMP
Sort: Most recent
```

**Create Custom Template**:
```
Template Name: "Secure Microservice Baseline"
Description: "FedRAMP High compliant microservice template with zero-trust networking"
Cloud: Azure Government
Services: AKS, SQL Database, Key Vault, Application Gateway
Compliance Level: FedRAMP High
Tags: microservices, zero-trust, production-ready
Files: [Upload or generate 35 files]
```

**Template Search Examples**:
```
"kubernetes azure sql fedramp"
→ Finds: Templates with AKS + SQL + FedRAMP compliance

"serverless aws lambda python"
→ Finds: AWS Lambda templates with Python runtime

"machine learning gpu training"
→ Finds: Templates with GPU-enabled compute for ML training
```

### Approval Workflows

**Approve Onboarding Request**:
```
Review Request: "Mission App Deployment"
Submitted by: CDR Johnson (Navy)
Classification: CUI
Resources: AKS (10 nodes), SQL (8 vCores), Storage (1TB)
Estimated cost: $4,200/month
Compliance: FedRAMP High (98% score)

Action: ✅ Approve with conditions:
- Limit AKS nodes to 8 (cost savings)
- Require quarterly compliance scans
- Set budget alert at $4,000/month

Comments: "Approved for production deployment. Ensure MFA is enforced 
for all admin access. Schedule security review in 90 days."
```

**Reject with Feedback**:
```
Action: ❌ Reject
Reason: Compliance gaps
Details:
- 5 critical FedRAMP controls not implemented (AC-2, IA-5, SC-7)
- Public blob access enabled (security risk)
- No encryption for data at rest configured

Required actions before resubmission:
1. Implement all critical controls
2. Disable public access on all storage accounts
3. Enable customer-managed encryption keys
4. Schedule security review with InfoSec team

Resubmit after: Remediation complete and validated
```

---

## 🎯 Advanced Techniques

### 1. Context Chaining

Build complex requirements through conversation:

**Turn 1**:
```
"I need to deploy a microservices application"
```

**Turn 2** (System asks):
```
"What cloud provider would you like to use?"
```

**Turn 3** (You provide):
```
"Azure Government for production, Azure Commercial for dev/staging"
```

**Turn 4** (System asks):
```
"How many microservices will you deploy?"
```

**Turn 5** (You provide):
```
"5 services: API gateway, auth service, order service, payment service, notification service"
```

**System generates**: Complete multi-service architecture with all 5 services

### 2. Referencing Previous Context

The system remembers conversation history:

```
User: "Deploy web app to Azure"
System: [Generates templates]

User: "Now add a database to that deployment"
System: [Updates templates to include Azure SQL]

User: "Make the database geo-replicated"
System: [Updates database config with geo-replication]

User: "What's the monthly cost for everything we've configured?"
System: "Estimated monthly cost: $1,850 (App Service $150, SQL Database $1,400, 
Storage $100, Networking $200)"
```

### 3. Template Inheritance

Build on existing templates:

```
"Use the 'Secure Microservice Baseline' template but:
- Change from Azure to AWS
- Replace AKS with EKS
- Use RDS PostgreSQL instead of Azure SQL
- Add CloudFront CDN
- Keep all FedRAMP compliance controls"
```

### 4. Batch Operations

Execute multiple operations in one prompt:

```
"Perform the following operations:
1. Create storage account 'data001' in rg-production
2. Create storage account 'data002' in rg-dr
3. Set up geo-replication between data001 and data002
4. Apply lifecycle policy: hot→cool after 30 days, cool→archive after 90 days
5. Enable soft delete with 30-day retention
6. Create managed identity 'app-identity' with read access to both storage accounts
7. Generate Bicep template for all resources
8. Create GitHub Action workflow to deploy the template"
```

### 5. Conditional Logic

Express complex requirements with conditions:

```
"Deploy application infrastructure with these rules:
- IF environment=production THEN:
  * Use Premium tier for all services
  * Enable geo-replication
  * Require manual approval for deployments
  * Set budget alert at $5000/month
- ELSE IF environment=staging THEN:
  * Use Standard tier
  * No geo-replication
  * Auto-deploy on merge to main
  * Set budget alert at $1000/month
- ELSE (development):
  * Use Basic tier where available
  * Single instance, no HA
  * Auto-deploy on any commit
  * Auto-shutdown at 8 PM weekdays, all day weekends
  * Set budget alert at $500/month"
```

---

## ✅ Best Practices

### 1. Start Broad, Then Narrow

❌ **Bad** (Too vague):
```
"Deploy my app"
```

✅ **Good** (Progressive detail):
```
"I need to deploy a web application"
→ System asks for details
→ You provide: "It's a .NET 8 API"
→ System asks for cloud
→ You provide: "Azure Government"
→ System asks for database
→ You provide: "SQL Server with geo-replication"
```

### 2. Include Classification Early

❌ **Bad** (Security as afterthought):
```
"Deploy app, oh and it's classified SECRET"
```

✅ **Good** (Security first):
```
"I need to deploy a SECRET classified application to Azure Government..."
```

### 3. Specify Compliance Requirements

❌ **Bad**:
```
"Make it secure"
```

✅ **Good**:
```
"Must meet FedRAMP High compliance with all 325 controls implemented"
```

### 4. Provide Cost Constraints

❌ **Bad**:
```
"Deploy the biggest cluster possible"
```

✅ **Good**:
```
"Deploy AKS cluster with monthly cost <$3000, optimizing for cost over performance"
```

### 5. Be Specific About Scale

❌ **Bad**:
```
"It needs to handle lots of users"
```

✅ **Good**:
```
"Expected: 50,000 concurrent users, 100M requests/day, 99.9% uptime SLA"
```

### 6. Mention Time Constraints

❌ **Bad**:
```
"Deploy when you can"
```

✅ **Good**:
```
"Need production deployment by end of quarter (60 days) for ATO deadline"
```

---

## 🐛 Troubleshooting Prompts

### Debugging Failed Deployments

**Get Deployment Logs**:
```
@platform show deployment logs for "payment-api-v2" that failed 15 minutes ago
```

**Analyze Failure**:
```
@platform diagnose why deployment "customer-portal-prod-20250109" failed:
- Show error messages
- Check resource quota limits
- Verify Azure permissions
- Review template validation errors
- Suggest remediation steps
```

### Performance Issues

**Diagnose Slow Performance**:
```
@platform investigate performance issues in production:
- Application: customer-portal
- Symptom: Response times >3s (normally <200ms)
- Started: 2 hours ago
- Check: Database queries, API dependencies, cache hit rates, 
  resource utilization, network latency
- Provide: Root cause analysis and recommended fixes
```

### Cost Surprises

**Unexpected Costs**:
```
@platform analyze why Azure costs increased 150% this month:
- Previous month: $5,000
- Current month: $12,500
- Show: Top 10 cost increases by resource
- Identify: New resources, size changes, increased usage
- Recommend: How to reduce back to $5,000-$6,000
```

### Compliance Failures

**Fix Compliance Issues**:
```
@platform auto-remediate compliance failures in resource group "prod-rg":
- Framework: FedRAMP High
- Severity: Critical and High only (don't touch Low/Medium)
- Dry run first: Show what will be changed
- After approval: Execute remediation
- Report: Before/after compliance scores
```

---

## 📝 Prompt Templates Library

### Copy-Paste Templates

#### New Service Deployment
```
I need to deploy [SERVICE_TYPE] named "[SERVICE_NAME]" for [ORGANIZATION].
I'm [RANK] [NAME] from [BRANCH].

Requirements:
- Cloud: [Azure Government | Azure Commercial | AWS | GCP]
- Region: [REGION]
- Classification: [UNCLASS | CUI | SECRET]
- Compliance: [FedRAMP High | FedRAMP Moderate | NIST 800-53]
- Services needed:
  * Compute: [AKS | EKS | App Service | Lambda | etc.]
  * Database: [SQL | PostgreSQL | Cosmos DB | DynamoDB]
  * Storage: [Blob | S3 | Cloud Storage]
  * Other: [List any additional services]
- Scale:
  * Users: [NUMBER] concurrent
  * Requests: [NUMBER] per day
  * Data: [SIZE] total
- Budget: $[AMOUNT] per month
- Timeline: Deploy by [DATE]
```

#### Security Hardening
```
Apply security hardening to [SCOPE] with:
- Encryption: [Customer-managed keys | Platform-managed | Both]
- Networking: [Private endpoints | VNet integration | Public with restrictions]
- Authentication: [Azure AD only | Managed Identity | Service Principal]
- Compliance: [FedRAMP | NIST | ISO 27001]
- Monitoring: [Enable all security logs | Basic | Custom]
- Remediation: [Auto-fix | Report only | Manual approval]
```

#### Cost Optimization
```
Optimize costs for [SCOPE]:
- Current spend: $[AMOUNT]/month
- Target: $[AMOUNT]/month ([PERCENTAGE]% reduction)
- Priorities: [Performance | Availability | Cost] (rank in order)
- Constraints: [Must maintain HA | Can't reduce security | etc.]
- Implementation: [Immediate | Phased over X weeks]
```

---

## 🎓 Learning Path

### Beginner (Week 1)
- ✅ Deploy first service using simple prompt
- ✅ Use chat interface for onboarding
- ✅ Review generated templates
- ✅ Deploy to development environment

### Intermediate (Week 2-3)
- ✅ Use VS Code extension @platform
- ✅ Multi-service deployments
- ✅ Add compliance requirements
- ✅ Cost optimization prompts

### Advanced (Week 4+)
- ✅ Context chaining for complex architectures
- ✅ Template inheritance and customization
- ✅ Batch operations
- ✅ Advanced security hardening

---

## 📞 Getting Help

If prompts aren't working as expected:

1. **Check syntax**: Review examples in this guide
2. **Add more context**: More details = better results
3. **Use progressive disclosure**: Build up complexity gradually
4. **Check logs**: View → Output → "Platform Engineering MCP"
5. **File issue**: Include prompt and expected vs actual behavior

---

**Pro Tip**: The AI learns from feedback. If a prompt doesn't work well, try rephrasing with more specific details. The system improves with clearer inputs!

---

*Last Updated: October 9, 2025*
