# RBAC and Authorization for Compliance Agent

## Overview

The Platform Engineering Copilot implements Role-Based Access Control (RBAC) for compliance operations using **Azure AD (Entra ID)** app roles and authorization policies. This ensures that only authorized users can execute sensitive operations like remediation execution, evidence collection, and document generation.

---

## Architecture

### 1. **Authentication Flow**

```
┌─────────────┐
│   User      │
│  (Browser)  │
└──────┬──────┘
       │ 1. Login to Azure AD
       ▼
┌─────────────────────┐
│   Azure AD          │
│  (Entra ID)         │
│                     │
│  - Validates user   │
│  - Checks MFA/CAC   │
│  - Issues JWT token │
└──────┬──────────────┘
       │ 2. JWT with roles claim
       ▼
┌─────────────────────┐
│  MCP Server         │
│  (HTTP Mode)        │
│                     │
│  - Validates token  │
│  - Extracts roles   │
│  - Enforces policies│
└──────┬──────────────┘
       │ 3. Authorized request
       ▼
┌─────────────────────┐
│ Compliance Plugin   │
│                     │
│  - Checks roles     │
│  - Logs audit entry │
│  - Executes action  │
└─────────────────────┘
```

### 2. **JWT Token Structure**

When a user authenticates with Azure AD, they receive a JWT token containing:

```json
{
  "aud": "api://platform-engineering-copilot",
  "iss": "https://login.microsoftonline.com/your-tenant-id/",
  "iat": 1732584000,
  "exp": 1732587600,
  "name": "John Doe",
  "oid": "user-object-id-abc123",
  "preferred_username": "john.doe@company.com",
  "roles": [
    "Compliance.Administrator",
    "Compliance.Analyst"
  ],
  "tid": "your-tenant-id"
}
```

---

## Defined Roles

### Compliance.Administrator
- **Description**: Full administrative access to all compliance operations
- **Permissions**:
  - Execute remediations (including critical findings)
  - Approve remediations
  - Collect and export evidence
  - Generate and export documents
  - Delete assessments and findings
  - Run assessments
  - View all compliance data

### Compliance.Auditor
- **Description**: Can view and audit compliance data, export evidence packages
- **Permissions**:
  - View assessments and findings
  - Collect and export evidence
  - Generate and export documents
  - Run assessments (read-only analysis)
  - **Cannot**: Execute remediations, delete data

### Compliance.Analyst
- **Description**: Can run assessments and execute approved remediations
- **Permissions**:
  - Run assessments
  - View findings
  - Collect evidence
  - Execute remediations (standard and high severity)
  - Update findings
  - **Cannot**: Approve critical remediations, delete assessments, export sensitive evidence

### Compliance.ReadOnly
- **Description**: Read-only access to compliance reports and findings
- **Permissions**:
  - View assessments
  - View findings
  - View documents
  - **Cannot**: Execute any modifications, exports, or remediations

---

## Authorization Policies

The following policies are configured in `Program.cs`:

| Policy Name | Required Roles | Purpose |
|-------------|----------------|---------|
| `CanExecuteRemediation` | Administrator, Analyst | Execute automated compliance remediations |
| `CanApproveRemediation` | Administrator | Approve critical remediations |
| `CanExportEvidence` | Administrator, Auditor | Export compliance evidence packages |
| `CanCollectEvidence` | Administrator, Auditor, Analyst | Collect compliance evidence artifacts |
| `CanDeleteAssessment` | Administrator | Delete compliance assessments |
| `CanRunAssessment` | Administrator, Auditor, Analyst | Run compliance assessments |
| `CanGenerateDocuments` | Administrator, Auditor, or custom claim | Generate ATO documents (SSP, SAR, POA&M) |
| `CanExportDocuments` | Administrator, Auditor | Export generated compliance documents |
| `CanUpdateFindings` | Administrator, Analyst | Update compliance findings |
| `CanDeleteFindings` | Administrator | Delete compliance findings |

---

## Azure AD Setup

### Step 1: Create App Registration

1. Navigate to **Azure Portal** → **Azure Active Directory** → **App Registrations**
2. Click **New registration**
3. Configure:
   - **Name**: `Platform Engineering Copilot`
   - **Supported account types**: `Accounts in this organizational directory only`
   - **Redirect URI**: `https://your-mcp-server.com/signin-oidc` (if using web flow)
4. Click **Register**

### Step 2: Define App Roles

1. In your App Registration, navigate to **App roles**
2. Click **Create app role** for each role:

**Compliance.Administrator:**
```json
{
  "allowedMemberTypes": ["User"],
  "description": "Full administrative access to compliance operations",
  "displayName": "Compliance Administrator",
  "id": "8e3af657-a8ff-443c-a75c-2fe8c4bcb635",
  "isEnabled": true,
  "value": "Compliance.Administrator"
}
```

**Compliance.Auditor:**
```json
{
  "allowedMemberTypes": ["User"],
  "description": "Can view and audit compliance data, export evidence",
  "displayName": "Compliance Auditor",
  "id": "9e3af657-a8ff-443c-a75c-2fe8c4bcb636",
  "isEnabled": true,
  "value": "Compliance.Auditor"
}
```

**Compliance.Analyst:**
```json
{
  "allowedMemberTypes": ["User"],
  "description": "Can run assessments and execute approved remediations",
  "displayName": "Compliance Analyst",
  "id": "ae3af657-a8ff-443c-a75c-2fe8c4bcb637",
  "isEnabled": true,
  "value": "Compliance.Analyst"
}
```

**Compliance.ReadOnly:**
```json
{
  "allowedMemberTypes": ["User"],
  "description": "Read-only access to compliance reports",
  "displayName": "Compliance ReadOnly",
  "id": "be3af657-a8ff-443c-a75c-2fe8c4bcb638",
  "isEnabled": true,
  "value": "Compliance.ReadOnly"
}
```

### Step 3: Expose API (for API access)

1. Navigate to **Expose an API**
2. Click **Add a scope**:
   - **Scope name**: `Compliance.Access`
   - **Who can consent**: `Admins only`
   - **Admin consent display name**: `Access compliance operations`
   - **Description**: `Allows the application to access compliance operations on behalf of the signed-in user`
3. Save

### Step 4: Configure Authentication

1. Navigate to **Authentication**
2. Add **Platform configurations**:
   - **Web**: `https://your-mcp-server.com/signin-oidc`
   - **Single-page application**: `http://localhost:3000` (for local dev)
3. **Implicit grant and hybrid flows**:
   - ☑ ID tokens (if using hybrid flow)
4. **Supported account types**: Single tenant
5. Save

---

## Assigning Roles to Users

### Via Azure Portal

1. Navigate to **Azure AD** → **Enterprise Applications**
2. Find your app: **Platform Engineering Copilot**
3. Click **Users and groups**
4. Click **Add user/group**
5. Select:
   - **Users**: Choose the user
   - **Select a role**: Choose the app role (e.g., `Compliance.Administrator`)
6. Click **Assign**

### Via Azure CLI

```bash
# Get your app's service principal ID
APP_NAME="Platform Engineering Copilot"
SP_ID=$(az ad sp list --display-name "$APP_NAME" --query "[0].id" -o tsv)

# Get the user's object ID
USER_EMAIL="john.doe@company.com"
USER_ID=$(az ad user show --id "$USER_EMAIL" --query "id" -o tsv)

# Get the app role ID (use the UUID from app manifest)
ROLE_ID="8e3af657-a8ff-443c-a75c-2fe8c4bcb635"  # Compliance.Administrator

# Assign the role
az rest --method POST \
  --uri "https://graph.microsoft.com/v1.0/users/$USER_ID/appRoleAssignments" \
  --headers "Content-Type=application/json" \
  --body "{
    \"principalId\": \"$USER_ID\",
    \"resourceId\": \"$SP_ID\",
    \"appRoleId\": \"$ROLE_ID\"
  }"
```

### Via PowerShell

```powershell
# Connect to Azure AD
Connect-AzureAD

# Get the service principal
$sp = Get-AzureADServicePrincipal -Filter "displayName eq 'Platform Engineering Copilot'"

# Get the user
$user = Get-AzureADUser -ObjectId "john.doe@company.com"

# Get the app role
$appRole = $sp.AppRoles | Where-Object { $_.Value -eq "Compliance.Administrator" }

# Assign the role
New-AzureADUserAppRoleAssignment `
  -ObjectId $user.ObjectId `
  -PrincipalId $user.ObjectId `
  -ResourceId $sp.ObjectId `
  -Id $appRole.Id
```

---

## Configuration

### appsettings.json

```json
{
  "AzureAD": {
    "Instance": "https://login.microsoftonline.com/",    
    "Audience": "api://your-app-client-id",
    "RequireMfa": true,
    "RequireCac": false
  }
}
```

### Environment Variables

For production, store sensitive values in Azure Key Vault:

```bash
# Store in Key Vault
az keyvault secret set \
  --vault-name pec-compliance-kv \
  --name AzureAD--TenantId \
  --value "your-tenant-id"

az keyvault secret set \
  --vault-name pec-compliance-kv \
  --name AzureAD--ClientId \
  --value "your-client-id"
```

---

## Testing Authorization

### Test 1: Verify Token Claims

```bash
# Get an access token
TOKEN=$(az account get-access-token --resource "api://your-app-client-id" --query accessToken -o tsv)

# Decode JWT (using jwt.io or jwt-cli)
echo $TOKEN | jwt decode -

# Verify "roles" claim contains your assigned roles
```

### Test 2: Test Remediation Execution (Analyst Role)

```bash
# Call MCP server with Bearer token
curl -X POST https://your-mcp-server.com/api/compliance/remediation \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "subscriptionId": "sub-123",
    "findingId": "finding-456",
    "dryRun": true
  }'

# Expected: 200 OK (if user has Compliance.Analyst or Compliance.Administrator role)
# Expected: 403 Forbidden (if user lacks role)
```

### Test 3: Test Evidence Export (Requires Auditor Role)

```bash
curl -X GET "https://your-mcp-server.com/api/compliance/evidence?subscriptionId=sub-123&controlFamily=AC" \
  -H "Authorization: Bearer $TOKEN"

# Expected: 200 OK (if user has Compliance.Auditor or Compliance.Administrator)
# Expected: 403 Forbidden (if user only has Compliance.Analyst)
```

---

## Audit Logging

All compliance operations are automatically audited with user context:

```json
{
  "entryId": "audit-789",
  "timestamp": "2025-11-25T10:30:00Z",
  "eventType": "RemediationExecuted",
  "actorId": "user-oid-abc123",
  "actorName": "john.doe@company.com",
  "action": "Execute",
  "resourceId": "sub-123/findings/finding-456",
  "severity": "High",
  "result": "Success",
  "metadata": {
    "subscriptionId": "sub-123",
    "findingId": "finding-456",
    "dryRun": false,
    "requireApproval": true
  },
  "complianceContext": {
    "requiresReview": true,
    "controlIds": ["AC-2", "AC-6", "AU-2", "AU-3"]
  }
}
```

### Querying Audit Logs

```csharp
// Get all remediation executions by a specific user
var auditQuery = new AuditSearchQuery
{
    EventType = "RemediationExecuted",
    ActorId = "user-oid-abc123",
    StartDate = DateTimeOffset.UtcNow.AddDays(-30),
    PageSize = 50
};

var results = await auditLoggingService.SearchAsync(auditQuery);
```

---

## Middleware Flow

The authorization middleware executes in this order:

1. **UseAuthentication()** - Validates JWT token, extracts claims
2. **UseAuthorization()** - Enforces authorization policies
3. **UseComplianceAuthorization()** - Logs compliance-specific audit entries
4. **CompliancePlugin** - Performs function-level role checks

```csharp
// In Program.cs
if (!string.IsNullOrEmpty(azureAdOptions.TenantId))
{
    app.UseAuthentication();           // Step 1: Validate token
    app.UseAuthorization();            // Step 2: Check policies
    app.UseComplianceAuthorization();  // Step 3: Audit access
    app.UseUserTokenAuthentication();  // Step 4: Extract user context
}
```

---

## Security Best Practices

### 1. **Principle of Least Privilege**
- Assign users the minimum role needed for their job function
- Use `Compliance.ReadOnly` for stakeholders who only need visibility
- Reserve `Compliance.Administrator` for security team leads

### 2. **Multi-Factor Authentication (MFA)**
- **Recommended**: Require MFA for all compliance operations
- Configure in `appsettings.json`: `"RequireMfa": true`
- For DoD environments: Use CAC/PIV authentication (`"RequireCac": true`)

### 3. **Conditional Access Policies**
In Azure AD, create conditional access policies:
- Require MFA for `Compliance.Administrator` role
- Restrict access to corporate network IP ranges
- Require compliant devices (Intune-managed)

### 4. **Privileged Identity Management (PIM)**
Use Azure PIM for time-limited elevation:
- Users request `Compliance.Administrator` role
- Approval required from security team
- Role automatically expires after 4 hours
- All elevations logged in Azure AD audit logs

### 5. **Regular Access Reviews**
- Quarterly review of role assignments
- Remove inactive users
- Audit `Compliance.Administrator` assignments

### 6. **Audit Log Retention**
- Retain audit logs for minimum 1 year (compliance requirement)
- Archive old logs to Azure Blob Storage
- Use Azure Log Analytics for long-term retention

---

## Troubleshooting

### Issue: "Token validation failed"

**Cause**: JWT token is expired or invalid

**Solution**:
```bash
# Get a fresh token
az account clear
az login
TOKEN=$(az account get-access-token --resource "api://your-app-client-id" --query accessToken -o tsv)
```

### Issue: "User lacks required role"

**Cause**: User not assigned app role in Azure AD

**Solution**:
1. Check role assignment in Azure Portal
2. Verify app role ID matches manifest
3. Re-assign role if needed
4. User must log out and back in to refresh token

### Issue: "Roles claim missing from token"

**Cause**: Token validation parameters incorrect

**Solution**:
```csharp
// In Program.cs, ensure RoleClaimType is set
options.TokenValidationParameters = new TokenValidationParameters
{
    RoleClaimType = "roles",  // Azure AD uses "roles" claim
    // ... other parameters
};
```

### Issue: "Authorization bypassed in stdio mode"

**Cause**: Running MCP server in stdio mode (for GitHub Copilot)

**Explanation**: Stdio mode does not support HTTP authentication. Authorization is only enforced in HTTP mode (`--http` flag).

**Solution**: For production, always run in HTTP mode:
```bash
dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http --port 5000
```

---

## Example Scenarios

### Scenario 1: Security Analyst Remediates Finding

1. **User**: Jane Smith (Compliance.Analyst role)
2. **Action**: Execute remediation for high-severity finding
3. **Flow**:
   - Jane logs in to Chat web app (Azure AD SSO)
   - JWT token issued with `"roles": ["Compliance.Analyst"]`
   - Jane runs: "Execute remediation for finding-123"
   - MCP server validates token
   - CompliancePlugin checks role (✅ Analyst can remediate)
   - Audit log created: "RemediationExecuted by jane.smith@company.com"
   - Remediation executes successfully

### Scenario 2: Auditor Exports Evidence Package

1. **User**: Bob Johnson (Compliance.Auditor role)
2. **Action**: Export evidence for AC control family
3. **Flow**:
   - Bob authenticates with MFA
   - Calls: "Collect evidence for AC family in subscription prod-123"
   - Authorization check passes (✅ Auditor can export evidence)
   - Evidence package generated
   - Audit log: "EvidenceCollected by bob.johnson@company.com" (severity: Warning)
   - Package encrypted and delivered

### Scenario 3: ReadOnly User Attempts Remediation

1. **User**: Alice Cooper (Compliance.ReadOnly role)
2. **Action**: Attempt to execute remediation
3. **Flow**:
   - Alice tries: "Execute remediation for finding-789"
   - Authorization check fails (❌ ReadOnly cannot remediate)
   - Error returned: "Unauthorized: User must have Compliance.Administrator or Compliance.Analyst role"
   - Audit log: "UnauthorizedAttempt by alice.cooper@company.com"

---

## Compliance Alignment

This RBAC implementation satisfies the following NIST 800-53 Rev 5 controls:

| Control | Title | Implementation |
|---------|-------|----------------|
| **AC-2** | Account Management | Azure AD user accounts with unique OIDs |
| **AC-3** | Access Enforcement | Authorization policies enforced via middleware |
| **AC-6** | Least Privilege | Role-based permissions with minimal access |
| **AU-2** | Audit Events | All compliance operations logged with user context |
| **AU-3** | Content of Audit Records | Audit logs include actor, action, timestamp, result |
| **AU-9** | Protection of Audit Information | Audit logs persisted to database, tamper-evident |
| **IA-2** | Identification and Authentication | Azure AD authentication required |
| **IA-2(1)** | Multi-Factor Authentication | MFA enforced via conditional access |

---

## Summary

✅ **Implemented:**
- Azure AD app roles (4 roles defined)
- Authorization policies (10 policies)
- User context service (extract user ID, roles, claims)
- Compliance authorization middleware (audit access)
- Function-level role checks (in CompliancePlugin)
- Audit logging with user context

✅ **Supported:**
- JWT Bearer token authentication
- Role-based access control
- MFA/CAC authentication
- Audit trail for all operations
- Stdio mode bypass (for AI tools)

📚 **Next Steps:**
1. Create app registration in Azure AD
2. Define app roles in manifest
3. Assign roles to users
4. Configure MFA/conditional access
5. Test authorization with each role
6. Review audit logs regularly

---

*Last Updated: November 25, 2025*  
*Version: 1.0*  
*Owner: Platform Engineering Team*
