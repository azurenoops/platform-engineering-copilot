# Setup Scripts for Platform Engineering Copilot

This directory contains helper scripts for setting up and deploying the Platform Engineering Copilot.

## Local Development Setup

### Quick Setup (Recommended)
Run in a PowerShell terminal:
```powershell
.\setup-local-quick.ps1
```

This script will:
1. Verify prerequisites (.NET, Docker, Azure CLI)
2. Check Azure authentication
3. Guide you through Azure OpenAI setup
4. Update your `.env` file
5. Build the solution
6. Start services with Docker Compose

## Individual Scripts

### Azure OpenAI Creation
If you need to create Azure OpenAI separately:
```powershell
.\create-openai-manual.ps1
```

Follow the Azure Portal instructions provided by the script.

## Manual Commands

If you prefer manual setup, see the commands in `SETUP-STATUS.md` in the root directory.

## Security Note

**NEVER commit `.env` files to git!** The `.env` file contains secrets and is gitignored for security.
