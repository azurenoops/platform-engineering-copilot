# Supervisor Platform Admin API

Admin API for platform engineers to manage service templates, infrastructure, and platform operations.

## 🚀 Overview

The Platform Admin API provides full CRUD operations for service templates and infrastructure management. This API powers the **Platform Engineering Copilot Admin Console** (web UI on port 5003) and can be used directly for automation and scripting.

### Access Methods

1. **🌐 Admin Console (Recommended)**: http://localhost:5003 - User-friendly web interface
2. **📡 Admin API**: http://localhost:5002 - RESTful API for automation
3. **📖 Swagger UI**: http://localhost:5002 - Interactive API documentation

## 🏗️ Architecture

```
Platform Engineering Copilot Admin Console (Port 5003) - Web UI
    ↓ HTTP
Platform Admin API (Port 5002) - REST API
    ↓
├── Template Management (Full CRUD)
├── Infrastructure Provisioning
├── Cost Estimation
└── Platform Statistics
    ↓
MCP ServiceTemplateTool (Read-Only)
├── List Templates
├── Get Template Details
└── Delete Templates
```

**Design Principle**: Create/Update operations require platform engineer expertise and should be done through the Admin Console or API. Chat/MCP tools provide read-only access with ability to delete (for cleanup).

## 📡 API Endpoints

### Template Management

#### Create Template
```http
POST /api/admin/templates
Content-Type: application/json

{
  "templateName": "my-api-service",
  "serviceName": "my-api",
  "description": "Python FastAPI microservice with PostgreSQL",
  "version": "0.7.1",
  "createdBy": "platform-engineer@example.com",
  "isPublic": false,
  "application": {
    "language": "Python",
    "framework": "FastAPI",
    "type": "API",
    "port": 8000
  },
  "databases": [
    {
      "name": "maindb",
      "type": "PostgreSQL",
      "version": "15",
      "location": "Cloud",
      "tier": "Standard",
      "storageGB": 50
    }
  ],
  "infrastructure": {
    "format": "Kubernetes",
    "provider": "Azure",
    "region": "eastus",
    "computePlatform": "Kubernetes"
  },
  "deployment": {
    "replicas": 3,
    "autoScaling": true,
    "minReplicas": 2,
    "maxReplicas": 10
  },
  "security": {
    "rbac": true,
    "tls": true,
    "networkPolicies": false
  },
  "observability": {
    "prometheus": true,
    "grafana": true
  }
}
```

**Response**:
```json
{
  "success": true,
  "templateId": "guid-here",
  "templateName": "my-api-service",
  "generatedFiles": [
    "main.py",
    "routers/health.py",
    "Dockerfile",
    "k8s/deployment.yaml",
    "k8s/service.yaml",
    "k8s/hpa.yaml"
  ],
  "summary": "Generated 12 files for my-api service",
  "componentsGenerated": [
    "Application Code",
    "Database Templates",
    "Kubernetes Manifests"
  ]
}
```

#### Update Template
```http
PUT /api/admin/templates/{templateId}
Content-Type: application/json

{
  "description": "Updated description",
  "version": "1.1.0",
  "isActive": true,
  "templateGenerationRequest": {
    // Optional: Include to regenerate files
    "serviceName": "my-api",
    "application": { ... },
    "databases": [ ... ]
  }
}
```

#### List Templates
```http
GET /api/admin/templates?search=python

Response:
{
  "count": 5,
  "templates": [
    {
      "id": "guid",
      "name": "my-api-service",
      "templateType": "Python",
      "version": "0.7.1",
      "isActive": true,
      "createdAt": "2025-10-01T10:00:00Z"
    }
  ]
}
```

#### Get Template
```http
GET /api/admin/templates/{templateId}
```

#### Delete Template
```http
DELETE /api/admin/templates/{templateId}
```

#### Validate Template
```http
POST /api/admin/templates/validate
Content-Type: application/json

{
  "serviceName": "test-service",
  "application": { ... },
  "databases": [ ... ],
  "deployment": { ... }
}

Response:
{
  "isValid": true,
  "errors": [],
  "warnings": [
    "TLS is disabled - not recommended for production"
  ]
}
```

#### Get Statistics
```http
GET /api/admin/templates/stats

Response:
{
  "totalTemplates": 25,
  "activeTemplates": 20,
  "inactiveTemplates": 5,
  "publicTemplates": 10,
  "privateTemplates": 15,
  "byType": [
    { "type": "Python", "count": 8 },
    { "type": "NodeJS", "count": 6 }
  ],
  "byCloudProvider": [
    { "provider": "Azure", "count": 20 },
    { "provider": "AWS", "count": 5 }
  ]
}
```

#### Bulk Operations
```http
POST /api/admin/templates/bulk
Content-Type: application/json

{
  "templateIds": ["guid1", "guid2", "guid3"],
  "operation": "activate"  // or "deactivate", "delete"
}

Response:
{
  "success": true,
  "totalRequested": 3,
  "succeeded": 3,
  "failed": 0,
  "failedTemplateIds": [],
  "errors": {}
}
```

### Infrastructure Management

#### Provision Infrastructure
```http
POST /api/admin/infrastructure/provision
Content-Type: application/json

{
  "resourceGroupName": "my-rg",
  "location": "eastus",
  "subscriptionId": "sub-id",
  "infrastructureType": "AKS",
  "tags": {
    "Environment": "Production",
    "ManagedBy": "PlatformTeam"
  },
  "configuration": {
    "nodeCount": 3,
    "vmSize": "Standard_D4s_v3"
  }
}
```

#### Get Resource Group Status
```http
GET /api/admin/infrastructure/resource-groups/{resourceGroup}/status
```

#### List Resource Groups
```http
GET /api/admin/infrastructure/resource-groups?subscription=sub-id
```

#### Delete Resource Group
```http
DELETE /api/admin/infrastructure/resource-groups/{resourceGroup}?force=false
```

#### Cost Estimate
```http
POST /api/admin/infrastructure/cost-estimate
Content-Type: application/json

{
  "infrastructureType": "AKS",
  "configuration": {
    "nodeCount": 3,
    "vmSize": "Standard_D4s_v3"
  }
}

Response:
{
  "estimatedMonthlyCost": 1250.00,
  "currency": "USD",
  "breakdown": [
    { "service": "Azure Kubernetes Service", "cost": 500.00 },
    { "service": "Azure SQL Database", "cost": 400.00 }
  ]
}
```

## 🔧 Running the Admin API

### Start the API
```bash
cd src/Platform.Engineering.Copilot.Admin
dotnet run
```

The API will start on `http://localhost:5002`

### Access Swagger UI
Open your browser to: `http://localhost:5002`

The Swagger UI provides interactive API documentation and testing.

## 🎯 Usage Examples

### Example 1: Create .NET Microservice with SQL Server
```bash
curl -X POST http://localhost:5002/api/admin/templates \
  -H "Content-Type: application/json" \
  -d '{
    "templateName": "dotnet-api-sqlserver",
    "serviceName": "order-service",
    "description": "Order management API with SQL Server",
    "application": {
      "language": "DotNet",
      "type": "API",
      "port": 8080
    },
    "databases": [{
      "name": "orderdb",
      "type": "AzureSQL",
      "location": "Cloud",
      "storageGB": 50
    }],
    "infrastructure": {
      "format": "Bicep",
      "provider": "Azure",
      "computePlatform": "AppService"
    }
  }'
```

### Example 2: Create Node.js + MongoDB Template
```bash
curl -X POST http://localhost:5002/api/admin/templates \
  -H "Content-Type: application/json" \
  -d '{
    "templateName": "nodejs-mongodb-api",
    "serviceName": "user-service",
    "description": "User management API with MongoDB",
    "application": {
      "language": "NodeJS",
      "type": "API",
      "port": 3000
    },
    "databases": [{
      "name": "userdb",
      "type": "CosmosDB",
      "location": "Cloud"
    }],
    "infrastructure": {
      "format": "Kubernetes",
      "computePlatform": "AKS"
    }
  }'
```

### Example 3: Infrastructure-Only Template
```bash
curl -X POST http://localhost:5002/api/admin/templates \
  -H "Content-Type: application/json" \
  -d '{
    "templateName": "data-platform-infra",
    "serviceName": "data-platform",
    "description": "Multi-database data platform",
    "databases": [
      {
        "name": "analyticsdb",
        "type": "PostgreSQL",
        "location": "Cloud",
        "storageGB": 100
      },
      {
        "name": "cachedb",
        "type": "Redis",
        "location": "Cloud"
      }
    ],
    "infrastructure": {
      "format": "Terraform",
      "provider": "Azure"
    }
  }'
```

## 🔐 Security Considerations

### Current State
- **Development Mode**: No authentication required
- **Port 5002**: Admin API (full access)
- **MCP Tools**: Read-only access (list, get, delete)

### Production Recommendations
1. **Add Authentication**: 
   - Azure AD integration
   - API keys for service accounts
   - Role-based access control (RBAC)

2. **Network Security**:
   - Restrict Admin API to internal network
   - Use Azure Private Endpoints
   - Implement rate limiting

3. **Audit Logging**:
   - Log all template create/update/delete operations
   - Track who accessed what and when
   - Store audit logs in secure location

## 📊 Monitoring & Operations

### Health Check
```http
GET /health
```

### Metrics
- Template creation/update/delete counts
- API response times
- Error rates
- Active template count

### Logging
All operations are logged with structured logging:
```
[Admin API] Creating template dotnet-api-sqlserver
[Admin API] Template created successfully with ID: guid-here
[Admin API] Listing templates. Search: python
```

## 🔄 Integration with MCP Tools

The `ServiceTemplateToolReadOnly` provides read-only access via MCP:

```json
{
  "action": "list",
  "search": "python"
}
```

For create/update operations, it directs users to the Admin API:
> ℹ️ **To create new templates**, use the Platform Admin API at `http://localhost:5002`

## 🎓 Best Practices

### Template Naming
- Use lowercase with hyphens: `my-api-service`
- Include language/framework: `python-fastapi-template`
- Be descriptive: `nodejs-express-postgres-api`

### Versioning
- Follow semantic versioning: `1.0.0`
- Increment version on updates
- Document changes in description

### Organization
- Use public templates for standard patterns
- Keep private templates for sensitive configurations
- Tag templates with metadata

### Testing
1. Validate template before creating
2. Test generated files in dev environment
3. Review infrastructure costs
4. Verify security settings

## 🚦 Next Steps

1. **Add Authentication**: Implement Azure AD or API key authentication
2. **Add Approval Workflow**: Require approval for production templates
3. **Add Version History**: Track template changes over time
4. **Add Template Sharing**: Share templates between teams
5. **Add Cost Alerts**: Alert when estimated costs exceed thresholds
6. **Create UI**: Build React/Blazor admin dashboard

## 📝 Notes

- Admin API runs on port **5002**
- MCP server runs on port **5100**
- Chat App runs on port **5001**
- All APIs share the same `appsettings.json` configuration
- Database: SQLite `platform_engineering_copilot_management.db`

## 🤝 Contributing

When adding new features to the Admin API:

1. Add service method in `Services/TemplateAdminService.cs`
2. Add controller endpoint in `Controllers/TemplateAdminController.cs`
3. Update this README with new endpoint documentation
4. Add request/response models to `Models/AdminModels.cs`
5. Test with Swagger UI
