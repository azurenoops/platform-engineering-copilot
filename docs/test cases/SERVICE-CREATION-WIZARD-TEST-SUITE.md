# Service Creation Wizard Test Suite

## Overview

This document contains comprehensive test cases for the **Service Creation Wizard** - an interactive 8-step workflow for creating DoD-compliant service repositories with IL2-IL6 compliance support.

**Agent:** Infrastructure Agent  
**Plugin:** `ServiceWizardPlugin`  
**Primary Functions:**
- `start_service_wizard` - Begin new wizard session
- `wizard_next_step` - Submit answer and advance
- `wizard_go_back` - Return to previous step
- `wizard_start_over` - Cancel and restart
- `wizard_status` - Check session progress
- `wizard_help` - Get help on DoD terms
- `list_wizard_sessions` - View all sessions
- `wizard_generate` - Generate final templates

---

## Test Categories

1. [Wizard Lifecycle](#1-wizard-lifecycle)
2. [8-Step Workflow](#2-8-step-workflow)
3. [Validation & Error Handling](#3-validation--error-handling)
4. [Session Management](#4-session-management)
5. [Help System](#5-help-system)
6. [Template Generation](#6-template-generation)
7. [Navigation & Control](#7-navigation--control)
8. [Impact Level Compliance](#8-impact-level-compliance)
9. [Edge Cases](#9-edge-cases)
10. [Integration Tests](#10-integration-tests)

---

## 1. Wizard Lifecycle

### Test 1.1: Start New Wizard Session
**Input:**
```
Create a new service
```

**Expected Behavior:**
- Wizard starts automatically
- Returns welcome message
- Displays Step 1 prompt (Mission Sponsor)
- Provides unique session ID
- Session saved with `InProgress` status

**Validation:**
- ✅ Session ID is GUID format
- ✅ Current step is `Step1_MissionSponsor`
- ✅ Welcome message includes all 8 steps
- ✅ Instructions for saving session ID provided

---

### Test 1.2: Complete Full Wizard (Happy Path)
**Input Sequence:**
```
Step 1: PMW-150 Cybersecurity
Step 2: IL5
Step 3: usgovvirginia
Step 4: CUI
Step 5: production
Step 6: N00244
Step 7: API - Threat Intelligence Service - REST API for threat intelligence feeds
Step 8: C# - .NET 8 - PostgreSQL - Bicep - AKS
```

**Expected Behavior:**
- Each step validates input
- Advances to next step automatically
- After Step 8, shows completion summary
- Offers to generate templates
- Session marked as `Complete`

**Validation:**
- ✅ All 8 fields captured correctly
- ✅ Progress shows 100%
- ✅ Completion summary includes all details
- ✅ Generation options presented

---

### Test 1.3: Resume Existing Session
**Input:**
```
Continue wizard session abc123...
```

**Expected Behavior:**
- Loads saved session state
- Returns prompt for current incomplete step
- Preserves all previous answers
- Shows progress percentage

**Validation:**
- ✅ Session restored with all data
- ✅ Current step matches saved state
- ✅ Previous answers intact

---

## 2. 8-Step Workflow

### Test 2.1: Step 1 - Mission Sponsor
**Input:**
```
Step 1: SPAWAR Atlantic
```

**Expected Behavior:**
- Accepts valid mission sponsor names
- Validates against known sponsors
- Advances to Step 2 (Impact Level)

**Valid Inputs:**
- PMW-XXX (any number)
- SPAWAR, NAVAIR, NAVSEA, NIWC
- Skip (optional)

**Validation:**
- ✅ Accepts PMW-120, PMW-150, etc.
- ✅ Accepts SPAWAR variations
- ✅ "skip" moves forward without setting
- ❌ Rejects obviously invalid (e.g., "asdfasdf")

---

### Test 2.2: Step 2 - Impact Level
**Input:**
```
Step 2: IL5
```

**Expected Behavior:**
- Accepts IL2, IL4, IL5, IL6
- Explains impact level requirements
- Advances to Step 3 (Region)

**Valid Inputs:**
- IL2, IL4, IL5, IL6
- Impact Level 2, Impact Level 5, etc.

**Validation:**
- ✅ Normalizes "Impact Level 5" → IL5
- ✅ Validates only valid levels
- ❌ Rejects IL1, IL3, IL7

---

### Test 2.3: Step 3 - Azure Region
**Input:**
```
Step 3: usgovvirginia
```

**Expected Behavior:**
- Accepts Azure Government regions
- Shows region recommendations based on IL
- Advances to Step 4 (Data Classification)

**Valid Inputs:**
- usgovvirginia
- usgovarizona
- usgov* variants

**Validation:**
- ✅ IL5/IL6 suggests usgovvirginia
- ✅ IL2/IL4 accepts commercial regions
- ✅ Normalizes case and spacing

---

### Test 2.4: Step 4 - Data Classification
**Input:**
```
Step 4: CUI
```

**Expected Behavior:**
- Accepts classification levels
- Validates against impact level
- Advances to Step 5 (Environment)

**Valid Inputs:**
- Unclassified
- CUI (Controlled Unclassified Information)
- Secret
- Top Secret

**Validation:**
- ✅ IL2 → Unclassified only
- ✅ IL4 → Unclassified, CUI
- ✅ IL5 → CUI, Secret
- ✅ IL6 → Secret, Top Secret
- ❌ Rejects mismatched IL/classification

---

### Test 2.5: Step 5 - Environment
**Input:**
```
Step 5: production
```

**Expected Behavior:**
- Accepts environment types
- Advances to Step 6 (DoDAAC)

**Valid Inputs:**
- dev, development
- test, testing, staging
- prod, production

**Validation:**
- ✅ Normalizes variations
- ✅ Case-insensitive

---

### Test 2.6: Step 6 - DoDAAC (Optional)
**Input:**
```
Step 6: N00244
```

**Expected Behavior:**
- Accepts DoDAAC codes
- Allows skip
- Advances to Step 7 (Service Type)

**Valid Inputs:**
- N00244 (6-character format)
- skip (optional field)

**Validation:**
- ✅ Validates format (letter + 5 digits)
- ✅ Optional - can skip
- ❌ Rejects invalid formats

---

### Test 2.7: Step 7 - Service Type & Name
**Input:**
```
Step 7: Web Application - Mission Portal - User-facing mission management portal
```

**Expected Behavior:**
- Parses service type, name, description
- Validates service type against known types
- Advances to Step 8 (Tech Stack)

**Valid Service Types:**
- API, Web Application, Worker Service
- Database, Message Queue, Cache
- Microservice, Batch Processing

**Validation:**
- ✅ Extracts type, name, description
- ✅ Handles multi-word types
- ✅ Requires name

---

### Test 2.8: Step 8 - Tech Stack
**Input:**
```
Step 8: Python - FastAPI - PostgreSQL - Terraform - AKS
```

**Expected Behavior:**
- Parses language, framework, database, IaC, platform
- Shows completion summary
- Offers template generation
- Marks session complete

**Valid Inputs:**
- Languages: C#, Python, Java, TypeScript, Go
- Frameworks: .NET, FastAPI, Spring Boot, Express, Gin
- Databases: PostgreSQL, SQL Server, CosmosDB, Redis
- IaC: Bicep, Terraform, ARM
- Platforms: AKS, App Service, VMs, Container Instances

**Validation:**
- ✅ Parses all components
- ✅ Uses intelligent defaults if missing
- ✅ Session marked complete
- ✅ Generation prompt appears

---

## 3. Validation & Error Handling

### Test 3.1: Invalid Impact Level
**Input:**
```
Step 2: IL3
```

**Expected Behavior:**
- Rejects invalid impact level
- Shows valid options (IL2, IL4, IL5, IL6)
- Remains on Step 2
- Does not advance

**Validation:**
- ❌ IL3 rejected
- ✅ Error message clear
- ✅ Stays at current step

---

### Test 3.2: Classification Mismatch
**Input:**
```
Step 2: IL2
Step 4: Secret
```

**Expected Behavior:**
- Detects classification exceeds impact level
- Shows error explaining mismatch
- Suggests valid classifications for IL2
- Remains on Step 4

**Validation:**
- ❌ Secret rejected for IL2
- ✅ Suggests "Unclassified" only
- ✅ Explains IL requirements

---

### Test 3.3: Invalid Region for Impact Level
**Input:**
```
Step 2: IL6
Step 3: eastus
```

**Expected Behavior:**
- Detects commercial region for IL6
- Requires Azure Government region
- Shows error with valid regions
- Remains on Step 3

**Validation:**
- ❌ Commercial region rejected for IL6
- ✅ Suggests usgovvirginia only
- ✅ Explains compliance requirement

---

### Test 3.4: Empty/Missing Answer
**Input:**
```
Step 1: [empty]
```

**Expected Behavior:**
- Detects empty input
- For required fields, prompts again
- For optional fields (DoDAAC), allows skip
- Provides guidance

**Validation:**
- ❌ Empty on required field rejected
- ✅ Clear prompt to provide value
- ✅ "skip" works on optional fields

---

### Test 3.5: Invalid Session ID
**Input:**
```
Continue wizard session invalid-xyz
```

**Expected Behavior:**
- Session not found
- Clear error message
- Suggests starting new wizard
- Lists available sessions

**Validation:**
- ❌ Session not loaded
- ✅ Helpful error message
- ✅ Alternative actions suggested

---

## 4. Session Management

### Test 4.1: List All Wizard Sessions
**Input:**
```
List all wizard sessions
```

**Expected Behavior:**
- Shows all sessions (up to 10 recent)
- Displays session ID (truncated)
- Shows status (In Progress / Complete)
- Shows service name
- Shows current step
- Shows completion percentage
- Shows last updated timestamp

**Validation:**
- ✅ Lists multiple sessions
- ✅ Truncates long session IDs
- ✅ Shows progress %
- ✅ Sorted by last updated

---

### Test 4.2: Check Session Status
**Input:**
```
Check wizard status abc123...
```

**Expected Behavior:**
- Shows session summary
- Lists all completed steps with values
- Shows current step
- Shows remaining steps
- Displays completion percentage

**Validation:**
- ✅ All captured data displayed
- ✅ Progress accurate
- ✅ Current step highlighted

---

### Test 4.3: Multiple Concurrent Sessions
**Input:**
```
Session 1: Start wizard A
Session 2: Start wizard B
Session 1: Continue with answer
Session 2: Continue with answer
```

**Expected Behavior:**
- Each session maintains independent state
- No cross-contamination
- Session IDs differentiate
- Both can progress independently

**Validation:**
- ✅ Session A and B separate
- ✅ Answers stored in correct session
- ✅ No data leakage between sessions

---

### Test 4.4: Delete Old Sessions
**Input:**
```
Delete wizard session abc123...
```

**Expected Behavior:**
- Session removed from storage
- Confirmation message
- Session no longer in list

**Validation:**
- ✅ Session deleted
- ✅ Not in list_wizard_sessions
- ❌ Cannot resume deleted session

---

## 5. Help System

### Test 5.1: Get Help - DoDAAC
**Input:**
```
What is DoDAAC?
```

**Expected Behavior:**
- Returns detailed DoDAAC explanation
- Explains format (6 characters)
- Provides examples
- Explains purpose

**Validation:**
- ✅ Contains definition
- ✅ Shows format
- ✅ Provides examples

---

### Test 5.2: Get Help - Impact Level
**Input:**
```
Explain Impact Level
```

**Expected Behavior:**
- Explains IL2, IL4, IL5, IL6
- Shows differences
- Explains requirements for each
- Links to data classification

**Validation:**
- ✅ All levels explained
- ✅ Security requirements listed
- ✅ Clear distinctions

---

### Test 5.3: Get Help - FIPS 140-2
**Input:**
```
What is FIPS 140-2?
```

**Expected Behavior:**
- Explains cryptographic module standard
- Shows which ILs require it
- Mentions Azure Key Vault HSM
- Explains compliance

**Validation:**
- ✅ Definition clear
- ✅ IL5+ requirement mentioned
- ✅ Azure implementation noted

---

### Test 5.4: Get Help - CAC
**Input:**
```
Explain CAC authentication
```

**Expected Behavior:**
- Explains Common Access Card
- Shows PIV certificate usage
- Indicates IL6 requirement
- Multi-factor authentication explanation

**Validation:**
- ✅ CAC definition
- ✅ Smart card technology
- ✅ IL6 connection

---

### Test 5.5: Get Help - ATO/RMF
**Input:**
```
What is ATO?
```

**Expected Behavior:**
- Explains Authority to Operate
- Connects to RMF process
- Shows required artifacts (SSP, SAR, POA&M)
- Mentions 3-year authorization period

**Validation:**
- ✅ ATO definition
- ✅ RMF 6-step process
- ✅ Artifact requirements

---

### Test 5.6: Get Help - eMASS
**Input:**
```
Explain eMASS
```

**Expected Behavior:**
- Explains Enterprise Mission Assurance Support Service
- Shows IL5+ requirement
- Explains continuous monitoring
- Links to ATO packages

**Validation:**
- ✅ eMASS purpose
- ✅ DoD system tracking
- ✅ IL5+ requirement

---

### Test 5.7: Get Help - Unknown Term
**Input:**
```
What is XYZ?
```

**Expected Behavior:**
- "No help available for 'XYZ'"
- Lists available help topics
- Suggests checking documentation

**Validation:**
- ✅ Graceful unknown term handling
- ✅ Available topics listed

---

## 6. Template Generation

### Test 6.1: Generate Templates After Completion
**Input:**
```
[Complete all 8 steps]
Generate templates
```

**Expected Behavior:**
- Creates comprehensive service repository
- Includes application code scaffolding
- Includes IaC templates (Bicep/Terraform)
- Includes Dockerfile
- Includes README with IL requirements
- Includes CI/CD pipeline
- Includes security documentation
- Compliance artifacts based on IL

**Validation:**
- ✅ Application code generated
- ✅ IaC templates match selected format
- ✅ Dockerfile optimized for IL
- ✅ Documentation comprehensive
- ✅ Compliance checklist included

---

### Test 6.2: IL5 Template Generation
**Input:**
```
Impact Level: IL5
Classification: CUI
Platform: AKS
```

**Expected Behavior:**
- Generates IL5-compliant templates
- Includes FIPS 140-2 configurations
- Network isolation enabled
- Private endpoints configured
- Azure Policy assignments
- STIG compliance checks
- Monitoring and logging
- Key Vault with HSM

**Validation:**
- ✅ FIPS mode enabled
- ✅ Private networking
- ✅ Encryption at rest/transit
- ✅ Azure Policy for IL5

---

### Test 6.3: IL6 Template Generation
**Input:**
```
Impact Level: IL6
Classification: Top Secret
Platform: AKS
```

**Expected Behavior:**
- Generates IL6-compliant templates
- CAC authentication configured
- Isolated compute (dedicated nodes)
- No public endpoints
- HSM-backed Key Vault
- Comprehensive audit logging
- usgovvirginia region enforced
- SCCA compliance architecture

**Validation:**
- ✅ CAC integration code
- ✅ Dedicated node pools
- ✅ Zero public access
- ✅ SCCA components

---

### Test 6.4: Generate with Terraform
**Input:**
```
IaC Format: Terraform
Platform: App Service
```

**Expected Behavior:**
- Generates .tf files
- Includes provider configuration
- Variables for IL parameters
- Modules for reusability
- State backend configuration
- Azure Gov provider for IL5+

**Validation:**
- ✅ Valid Terraform syntax
- ✅ Modular structure
- ✅ IL-appropriate resources
- ✅ Variables documented

---

### Test 6.5: Generate with Bicep
**Input:**
```
IaC Format: Bicep
Platform: AKS
```

**Expected Behavior:**
- Generates .bicep files
- Parameterized templates
- Modular structure
- Deployment scripts
- IL-compliant resources

**Validation:**
- ✅ Valid Bicep syntax
- ✅ Parameter files included
- ✅ Module organization
- ✅ Deployment guide

---

### Test 6.6: Generate Multiple Services
**Input:**
```
Service 1: API - Threat Intel
Service 2: Web App - Admin Portal
Service 3: Worker - Data Processor
```

**Expected Behavior:**
- Generates multi-service solution
- Shared infrastructure components
- Service-specific configurations
- Integration documentation
- Unified deployment

**Validation:**
- ✅ All services generated
- ✅ Shared resources (VNet, Key Vault)
- ✅ Service isolation maintained
- ✅ Integration patterns documented

---

## 7. Navigation & Control

### Test 7.1: Go Back to Previous Step
**Input:**
```
Step 1: PMW-150
Step 2: IL5
Go back
```

**Expected Behavior:**
- Returns to Step 1
- Previous answer preserved (PMW-150)
- Can change answer
- Can proceed forward again

**Validation:**
- ✅ Returns to Step 1
- ✅ Shows current value
- ✅ Can modify
- ✅ Can re-advance

---

### Test 7.2: Go Back Multiple Steps
**Input:**
```
Step 1-5 completed
Go back
Go back
Go back
```

**Expected Behavior:**
- Steps back through each step
- All answers preserved
- Can modify any step
- Progress percentage decreases

**Validation:**
- ✅ Steps back correctly
- ✅ All values intact
- ✅ Can edit any field

---

### Test 7.3: Start Over
**Input:**
```
[Partially complete wizard]
Start over
```

**Expected Behavior:**
- Confirms action
- Deletes current session
- Creates new session
- Returns to Step 1
- All previous data cleared

**Validation:**
- ✅ Confirmation prompt
- ✅ Old session deleted
- ✅ New session ID generated
- ✅ Fresh start

---

### Test 7.4: Cancel Wizard
**Input:**
```
Cancel wizard session abc123...
```

**Expected Behavior:**
- Confirms cancellation
- Deletes session
- Data not saved
- Returns to main chat

**Validation:**
- ✅ Confirmation required
- ✅ Session deleted
- ✅ Graceful exit

---

## 8. Impact Level Compliance

### Test 8.1: IL2 Configuration
**Input:**
```
Impact Level: IL2
Data Classification: Unclassified
```

**Expected Behavior:**
- Commercial Azure regions allowed
- Basic encryption at rest
- Standard authentication
- Basic monitoring
- No CAC required
- No eMASS required

**Validation:**
- ✅ Commercial region accepted
- ✅ Standard resources
- ✅ Basic security controls

---

### Test 8.2: IL4 Configuration
**Input:**
```
Impact Level: IL4
Data Classification: CUI
```

**Expected Behavior:**
- Azure Government preferred
- CUI encryption requirements
- Enhanced monitoring
- Network isolation recommended
- Azure Policy assignments

**Validation:**
- ✅ Azure Gov recommended
- ✅ CUI controls applied
- ✅ Enhanced security

---

### Test 8.3: IL5 Configuration
**Input:**
```
Impact Level: IL5
Data Classification: CUI/Secret
```

**Expected Behavior:**
- Azure Government required
- FIPS 140-2 cryptography
- Private networking required
- Key Vault with HSM
- Azure Defender enabled
- eMASS integration
- Comprehensive logging

**Validation:**
- ✅ Azure Gov enforced
- ✅ FIPS mode enabled
- ✅ Private endpoints
- ✅ eMASS artifacts

---

### Test 8.4: IL6 Configuration
**Input:**
```
Impact Level: IL6
Data Classification: Top Secret
```

**Expected Behavior:**
- usgovvirginia only
- CAC authentication required
- Isolated compute (dedicated hosts)
- Zero public access
- HSM encryption
- SCCA architecture
- Maximum audit logging
- Dedicated infrastructure

**Validation:**
- ✅ Region locked to usgovvirginia
- ✅ CAC code included
- ✅ Isolated resources
- ✅ SCCA compliant

---

### Test 8.5: IL Escalation (Change IL Mid-Wizard)
**Input:**
```
Step 2: IL2
Step 3: eastus
Go back to Step 2
Step 2: IL6
```

**Expected Behavior:**
- Re-validates dependent steps
- Flags incompatible region (eastus for IL6)
- Prompts to update region
- Explains requirements changed

**Validation:**
- ✅ Detects incompatibility
- ✅ Requires region change
- ✅ Explains IL6 requirements

---

## 9. Edge Cases

### Test 9.1: Very Long Service Name
**Input:**
```
Step 7: API - ThisIsAnExtremelyLongServiceNameThatExceedsReasonableLimitsAndShouldBeTruncatedOrRejected
```

**Expected Behavior:**
- Validates name length
- Warns if too long
- Suggests abbreviation
- May truncate with warning

**Validation:**
- ✅ Length validation
- ✅ Warning message
- ✅ Graceful handling

---

### Test 9.2: Special Characters in Name
**Input:**
```
Step 7: API - Threat@Intel#Service!
```

**Expected Behavior:**
- Detects invalid characters
- Sanitizes or rejects
- Explains naming rules
- Suggests valid alternative

**Validation:**
- ✅ Character validation
- ✅ Clear error message
- ✅ Naming guidelines

---

### Test 9.3: Duplicate Session Resume
**Input:**
```
[Two users try to resume same session ID]
```

**Expected Behavior:**
- Session locking mechanism
- Prevents concurrent modification
- Or allows read-only view
- Clear ownership indication

**Validation:**
- ✅ Concurrent access handled
- ✅ Data integrity maintained

---

### Test 9.4: Session Timeout
**Input:**
```
[Start session, wait 24 hours, resume]
```

**Expected Behavior:**
- Old sessions may expire
- Warning about stale data
- Option to continue or restart
- Data preserved if within retention

**Validation:**
- ✅ Timeout policy clear
- ✅ Graceful expiration handling

---

### Test 9.5: Unsupported Platform/IL Combination
**Input:**
```
Impact Level: IL6
Platform: Azure Functions
```

**Expected Behavior:**
- Detects unsupported combination
- Explains IL6 requires isolated compute
- Suggests supported platforms (AKS, VMs)
- Blocks invalid configuration

**Validation:**
- ❌ Functions rejected for IL6
- ✅ Explains requirement
- ✅ Suggests alternatives

---

## 10. Integration Tests

### Test 10.1: Wizard → Template Generation → Deployment
**Input:**
```
Complete wizard → Generate → Deploy
```

**Expected Behavior:**
- Complete wizard successfully
- Generate templates
- Templates are valid/deployable
- Deployment succeeds in Azure
- Resources match IL requirements

**Validation:**
- ✅ End-to-end workflow
- ✅ Templates deploy successfully
- ✅ Resources comply with IL

---

### Test 10.2: Wizard → Export Configuration
**Input:**
```
Complete wizard → Export config
```

**Expected Behavior:**
- Exports wizard state as JSON/YAML
- Can be imported later
- Can be version controlled
- Can be shared with team

**Validation:**
- ✅ Valid export format
- ✅ Re-importable
- ✅ Human-readable

---

### Test 10.3: Wizard → Compliance Scan
**Input:**
```
Complete wizard → Generate → Scan for compliance
```

**Expected Behavior:**
- Generated templates are scanned
- Compliance report shows IL requirements met
- No critical violations
- IL-appropriate controls confirmed

**Validation:**
- ✅ Compliance scan passes
- ✅ IL controls validated
- ✅ No security gaps

---

### Test 10.4: Wizard → Documentation Generation
**Input:**
```
Complete wizard → Generate docs
```

**Expected Behavior:**
- Generates comprehensive README
- Includes architecture diagrams
- Lists all compliance requirements
- Provides deployment guide
- Shows security controls
- IL-specific documentation

**Validation:**
- ✅ Complete documentation
- ✅ IL requirements documented
- ✅ Architecture clear
- ✅ Deployment steps accurate

---

### Test 10.5: Wizard → Cost Estimation
**Input:**
```
Complete wizard → Estimate cost
```

**Expected Behavior:**
- Analyzes selected resources
- Provides monthly cost estimate
- Shows IL impact on cost
- Compares IL2 vs IL5 vs IL6 costs
- Optimization suggestions

**Validation:**
- ✅ Cost estimate reasonable
- ✅ IL overhead explained
- ✅ Optimization tips provided

---

## Test Execution Checklist

### Pre-Conditions
- [ ] Infrastructure Agent deployed
- [ ] ServiceWizardPlugin registered
- [ ] Database/storage for sessions configured
- [ ] Template generator service available
- [ ] Azure credentials configured

### Execution Environment
- [ ] Test in Development environment
- [ ] Test in Staging environment
- [ ] Test session persistence
- [ ] Test with multiple users

### Post-Conditions
- [ ] All sessions cleaned up
- [ ] No orphaned resources
- [ ] Logs reviewed for errors
- [ ] Performance metrics collected

---

## Success Criteria

| Category | Pass Rate | Critical Tests |
|----------|-----------|----------------|
| Wizard Lifecycle | 100% | Test 1.2, 1.3 |
| 8-Step Workflow | 100% | All 2.x tests |
| Validation | 100% | All 3.x tests |
| Session Management | 100% | Test 4.1, 4.3 |
| Help System | 90% | Test 5.1-5.5 |
| Template Generation | 100% | Test 6.2, 6.3 |
| Navigation | 100% | Test 7.1, 7.3 |
| IL Compliance | 100% | All 8.x tests |
| Edge Cases | 85% | Test 9.5 |
| Integration | 100% | Test 10.1, 10.3 |

---

## Known Issues

| Issue ID | Description | Severity | Status |
|----------|-------------|----------|--------|
| WIZ-001 | Session timeout not configurable | Low | Open |
| WIZ-002 | No session export/import yet | Medium | Planned |
| WIZ-003 | Concurrent session editing | Low | Open |
| WIZ-004 | Limited to 10 recent sessions | Low | Design |

---

## Test Automation

### Automated Test Suite
```bash
# Run all wizard tests
dotnet test --filter "Category=ServiceWizard"

# Run only critical path tests
dotnet test --filter "Category=ServiceWizard&Priority=Critical"

# Run IL compliance tests
dotnet test --filter "Category=ServiceWizard&Category=Compliance"
```

### Performance Benchmarks
- Session creation: < 100ms
- Step validation: < 50ms
- Template generation: < 5s
- Session list: < 200ms (10 sessions)

---

## Appendices

### Appendix A: Valid Input Examples

**Mission Sponsors:**
- PMW-120, PMW-150, PMW-160
- SPAWAR, SPAWAR Atlantic, SPAWAR Pacific
- NAVAIR, NAVSEA, NIWC

**Impact Levels:**
- IL2, IL4, IL5, IL6

**Regions:**
- usgovvirginia, usgovarizona (IL4+)
- eastus, westus (IL2)

**Data Classifications:**
- Unclassified
- CUI (Controlled Unclassified Information)
- Secret
- Top Secret

**DoDAAC Format:**
- N00244 (letter + 5 digits)

### Appendix B: Error Message Examples

```
❌ Invalid Impact Level: "IL3"
Valid options: IL2, IL4, IL5, IL6

❌ Classification "Secret" exceeds Impact Level "IL2"
For IL2, only "Unclassified" is permitted.

❌ Region "eastus" not allowed for IL6
IL6 requires Azure Government: usgovvirginia

❌ Session not found: abc123...
Use 'list_wizard_sessions' to see available sessions.
```

### Appendix C: Related Documentation

- [Infrastructure Agent README](../../src/Platform.Engineering.Copilot.Infrastructure.Agent/README.md)
- [DoD Compliance Guide](../COMPLIANCE.md)
- [Template Generation Guide](../TEMPLATE-GENERATION.md)
- [Impact Level Requirements](../IMPACT-LEVELS.md)
