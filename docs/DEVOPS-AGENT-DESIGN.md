# DevOps Agent - Design Document
**Created:** March 12, 2026  
**Status:** Proposed Enhancement  
**Agent ID:** `devops`  
**Branch:** BT_deploy

---

## Overview

The **DevOps Agent** provides comprehensive source control, CI/CD, and work tracking capabilities for both GitHub and Azure DevOps Server/Services. This agent enables end-to-end platform engineering workflows including repository scaffolding, pipeline automation, and team management.

---

## Problem Statement

**Current Gaps:**
- No automated repository creation/scaffolding
- Manual CI/CD pipeline setup
- No work item/issue automation
- Limited team onboarding automation
- No unified interface for GitHub + Azure DevOps

**Impact:**
Platform engineers must manually configure source control, pipelines, and work tracking for each new project, slowing down service delivery.

---

## Proposed Solution

### DevOps Agent Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      DevOps Agent                            │
│  Name: "DevOps Agent"                                        │
│  ID: "devops"                                                │
│  Description: "GitHub and Azure DevOps automation"           │
├─────────────────────────────────────────────────────────────┤
│  GitHub Tools (10)              │  Azure DevOps Tools (10)   │
│  ├─ Repository Management (4)   │  ├─ Repository Mgmt (3)   │
│  ├─ Issues & PRs (3)            │  ├─ Work Items (3)        │
│  ├─ Actions & Workflows (2)     │  ├─ Pipelines (2)         │
│  └─ Team Management (1)         │  └─ Project Settings (2)  │
└─────────────────────────────────────────────────────────────┘
```

### Tool Catalog (20 total tools)

---

## GitHub Tools (10)

### Repository Management (4 tools)

#### 1. `create_github_repository`
**Description:** Create new GitHub repository with templates and compliance settings

**Parameters:**
- `org` (string) - Organization name
- `name` (string, required) - Repository name
- `description` (string) - Repository description
- `private` (bool, default: true) - Visibility
- `template` (string) - Template repository
- `topics` (array) - Repository topics/tags
- `auto_init` (bool, default: true) - Initialize with README
- `gitignore_template` (string) - .gitignore template
- `license` (string) - License type
- `branch_protection` (bool, default: true) - Enable branch protection
- `require_code_owners` (bool, default: false) - Require CODEOWNERS approval

**Returns:** Repository URL, clone URL, settings

**Example:**
```
"Create GitHub repo 'my-app' with .NET template and branch protection"
```

#### 2. `list_github_repositories`
**Description:** List repositories with filtering

**Parameters:**
- `org` (string) - Filter by organization
- `topic` (string) - Filter by topic
- `archived` (bool) - Include archived repos
- `visibility` (string) - public/private/internal

**Returns:** List of repositories with metadata

#### 3. `update_github_repository`
**Description:** Update repository settings, topics, protection rules

**Parameters:**
- `org` (string)
- `repo` (string, required)
- `settings` (object) - Settings to update

#### 4. `delete_github_repository`
**Description:** Delete GitHub repository (with confirmation)

**Parameters:**
- `org` (string)
- `repo` (string, required)
- `confirm` (string, required) - Must match repo name

---

### Issues & Pull Requests (3 tools)

#### 5. `create_github_issue`
**Description:** Create issue with labels and assignees

**Parameters:**
- `org` (string)
- `repo` (string, required)
- `title` (string, required)
- `body` (string)
- `labels` (array)
- `assignees` (array)
- `milestone` (number)

#### 6. `list_github_issues`
**Description:** List and filter issues/PRs

**Parameters:**
- `org` (string)
- `repo` (string, required)
- `state` (string) - open/closed/all
- `labels` (array)
- `assignee` (string)

#### 7. `create_github_pull_request`
**Description:** Create pull request

**Parameters:**
- `org` (string)
- `repo` (string, required)
- `title` (string, required)
- `head` (string, required) - Source branch
- `base` (string, required) - Target branch
- `body` (string)
- `draft` (bool)
- `reviewers` (array)

---

### Actions & Workflows (2 tools)

#### 8. `create_github_workflow`
**Description:** Create GitHub Actions workflow from templates

**Parameters:**
- `org` (string)
- `repo` (string, required)
- `name` (string, required) - Workflow name
- `template` (string) - Template type (dotnet-build, docker, bicep-deploy, etc.)
- `triggers` (array) - push, pull_request, schedule, workflow_dispatch
- `environment` (string) - Deployment environment

**Templates:**
- `dotnet-build` - .NET build and test
- `docker-build-push` - Docker build and push to ACR
- `bicep-deploy` - Bicep deployment to Azure
- `compliance-scan` - Security and compliance scanning
- `terraform-deploy` - Terraform deployment

#### 9. `list_github_workflow_runs`
**Description:** List workflow runs with status

**Parameters:**
- `org` (string)
- `repo` (string, required)
- `workflow` (string) - Workflow name
- `status` (string) - success/failure/in_progress

---

### Team Management (1 tool)

#### 10. `manage_github_team`
**Description:** Create/update teams and manage members

**Parameters:**
- `org` (string, required)
- `team` (string, required)
- `action` (string) - create/add_member/remove_member
- `members` (array)
- `permissions` (string) - pull/push/admin

---

## Azure DevOps Tools (10)

### Repository Management (3 tools)

#### 11. `create_ado_repository`
**Description:** Create Azure DevOps Git repository

**Parameters:**
- `organization` (string, required)
- `project` (string, required)
- `name` (string, required)
- `default_branch` (string, default: main)

#### 12. `list_ado_repositories`
**Description:** List repositories in project

**Parameters:**
- `organization` (string, required)
- `project` (string, required)

#### 13. `delete_ado_repository`
**Description:** Delete repository with confirmation

**Parameters:**
- `organization` (string, required)
- `project` (string, required)
- `repo` (string, required)
- `confirm` (string, required)

---

### Work Items (3 tools)

#### 14. `create_ado_work_item`
**Description:** Create work item (User Story, Task, Bug, Epic)

**Parameters:**
- `organization` (string, required)
- `project` (string, required)
- `type` (string, required) - UserStory/Task/Bug/Epic
- `title` (string, required)
- `description` (string)
- `assigned_to` (string)
- `area_path` (string)
- `iteration_path` (string)
- `tags` (array)

#### 15. `list_ado_work_items`
**Description:** Query work items with WIQL

**Parameters:**
- `organization` (string, required)
- `project` (string, required)
- `query` (string) - WIQL query or predefined filter
- `state` (string) - Active/Resolved/Closed

#### 16. `update_ado_work_item`
**Description:** Update work item fields

**Parameters:**
- `organization` (string, required)
- `project` (string, required)
- `id` (number, required)
- `fields` (object) - Fields to update

---

### Pipelines (2 tools)

#### 17. `create_ado_pipeline`
**Description:** Create Azure Pipeline from YAML template

**Parameters:**
- `organization` (string, required)
- `project` (string, required)
- `name` (string, required)
- `repository` (string, required)
- `yaml_path` (string) - Path to azure-pipelines.yml
- `template` (string) - Pipeline template type

**Templates:**
- `dotnet-ci` - .NET build and test
- `docker-ci-cd` - Docker build and ACR push
- `bicep-iac` - Infrastructure deployment
- `multi-stage` - Multi-stage deployment (dev/test/prod)

#### 18. `run_ado_pipeline`
**Description:** Trigger pipeline run

**Parameters:**
- `organization` (string, required)
- `project` (string, required)
- `pipeline_id` (number, required)
- `branch` (string, default: main)
- `parameters` (object) - Pipeline parameters

---

### Project Settings (2 tools)

#### 19. `create_ado_project`
**Description:** Create Azure DevOps project

**Parameters:**
- `organization` (string, required)
- `name` (string, required)
- `description` (string)
- `visibility` (string) - private/public
- `source_control` (string, default: Git)
- `template` (string) - Agile/Scrum/CMMI

#### 20. `manage_ado_team`
**Description:** Create/update teams and members

**Parameters:**
- `organization` (string, required)
- `project` (string, required)
- `team` (string, required)
- `action` (string) - create/add_member/remove_member
- `members` (array)

---

## Integration with Existing Agents

### Collaboration Patterns

**With Infrastructure Agent:**
```
User: "Create a new AKS application with complete DevOps setup"

Flow:
1. Infrastructure Agent: Generate Bicep templates
2. DevOps Agent: Create GitHub repo with templates
3. DevOps Agent: Create GitHub Actions workflow for deployment
4. DevOps Agent: Create Azure DevOps project for work tracking
5. Infrastructure Agent: Deploy infrastructure
```

**With Environment Agent:**
```
User: "Scaffold new microservice from template"

Flow:
1. Environment Agent: Get service template
2. DevOps Agent: Create GitHub repo
3. DevOps Agent: Push template code to repo
4. DevOps Agent: Create CI/CD pipeline
5. DevOps Agent: Create initial work items (setup tasks)
```

**With Compliance Agent:**
```
User: "Create repo with NIST compliance scanning"

Flow:
1. DevOps Agent: Create repository
2. DevOps Agent: Add compliance workflow (SAST, secrets scanning)
3. Compliance Agent: Configure policy checks
4. DevOps Agent: Enable branch protection with required checks
```

---

## Configuration

### appsettings.json Addition

```json
{
  "AgentConfiguration": {
    "DevOpsAgent": {
      "Enabled": true,
      "Temperature": 0.3,
      "MaxTokens": 4000,
      "GitHub": {
        "Enabled": true,
        "DefaultOrg": "your-org",
        "RequireBranchProtection": true,
        "RequireCodeOwners": false,
        "DefaultTemplates": {
          "Workflow": "dotnet-build",
          "GitIgnore": "VisualStudio",
          "License": "MIT"
        }
      },
      "AzureDevOps": {
        "Enabled": true,
        "DefaultOrganization": "https://dev.azure.com/your-org",
        "DefaultProject": "Platform",
        "DefaultProcessTemplate": "Agile",
        "RequirePullRequests": true
      }
    }
  }
}
```

### Environment Variables (.env)

```bash
# GitHub Configuration (already in .env)
GITHUB_TOKEN=ghp_your_personal_access_token
GITHUB_API_BASE_URL=https://api.github.com
GITHUB_DEFAULT_OWNER=your-org

# Azure DevOps Configuration (new)
AZURE_DEVOPS_TOKEN=your_ado_pat
AZURE_DEVOPS_ORGANIZATION=https://dev.azure.com/your-org
AZURE_DEVOPS_DEFAULT_PROJECT=Platform
```

---

## Implementation Plan

### Phase 1: Core Structure (2 hours)
- [ ] Create `DevOpsAgent.cs` extending `BaseAgent`
- [ ] Create `DevOpsAgent.prompt.txt` system prompt
- [ ] Register agent in `PlatformAgentGroupChat`
- [ ] Add configuration section

### Phase 2: GitHub Tools (4 hours)
- [ ] Implement GitHub repository tools (4)
- [ ] Implement GitHub issues/PRs tools (3)
- [ ] Implement GitHub Actions tools (2)
- [ ] Implement GitHub team tool (1)

### Phase 3: Azure DevOps Tools (4 hours)
- [ ] Implement ADO repository tools (3)
- [ ] Implement ADO work item tools (3)
- [ ] Implement ADO pipeline tools (2)
- [ ] Implement ADO project tools (2)

### Phase 4: Templates & Integration (2 hours)
- [ ] Create GitHub Actions workflow templates
- [ ] Create Azure Pipeline YAML templates
- [ ] Test multi-agent workflows
- [ ] Update documentation

### Total Estimated Time: 12 hours

---

## Example Use Cases

### Use Case 1: New Microservice Setup
```
User: "Create a new microservice called 'user-service' with full DevOps setup"

DevOps Agent will:
1. Create GitHub repo "user-service"
2. Initialize with .NET template
3. Add GitHub Actions workflow (build + deploy)
4. Create Azure DevOps project
5. Create initial backlog items
6. Set up team access
```

### Use Case 2: Compliance Workflow
```
User: "Add security scanning to all my repositories"

DevOps Agent will:
1. List all repositories
2. For each repo:
   - Add compliance-scan GitHub workflow
   - Enable branch protection
   - Add required status checks
   - Create security policy
```

### Use Case 3: Pipeline Migration
```
User: "Migrate pipelines from Azure DevOps to GitHub Actions"

DevOps Agent will:
1. List ADO pipelines
2. Convert YAML to GitHub Actions format
3. Create equivalent workflows in GitHub
4. Test and validate
```

---

## Benefits

✅ **Automation:** Reduce manual repo/pipeline setup from hours to minutes  
✅ **Consistency:** Standardized templates and configurations  
✅ **Compliance:** Built-in security and branch protection  
✅ **Integration:** Seamless multi-agent workflows  
✅ **Flexibility:** Support both GitHub and Azure DevOps  

---

## Next Steps

1. Review and approve design
2. Create feature branch: `feature/devops-agent`
3. Implement Phase 1 (core structure)
4. Test with basic GitHub operations
5. Expand with remaining tools
6. Document and merge to BT_deploy

---

**Ready to build this?** Let me know and I can start implementing! 🚀
