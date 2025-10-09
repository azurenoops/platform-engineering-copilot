# Platform Engineering Admin Console

Web-based admin interface for managing service templates and infrastructure.

## Overview

The Platform Engineering Admin Console provides a user-friendly interface for platform engineers to:

- **Create & Manage Templates**: Full CRUD operations for service templates
- **View Statistics**: Dashboard with template analytics
- **Manage Infrastructure**: Provision and manage Azure/AWS resources
- **Monitor Operations**: Track template usage and deployments

## Architecture

```
Port 7003: Admin Console UI (React)
    ↓ HTTP
Port 7002: Admin API (ASP.NET Core)
    ↓
Platform Services (Template Generator, Storage, etc.)
```

## Getting Started

### Prerequisites

- Node.js 16+ and npm
- .NET 9 SDK
- Admin API running on port 7002

### Installation

```bash
# Install npm packages
cd ClientApp
npm install

# Run in development mode
npm start
```

The app will open at http://localhost:3001 and proxy API requests to http://localhost:7002.

### Running with .NET

```bash
# From the project root
dotnet run
```

The app will be served at http://localhost:7003 with the React app integrated.

## Features

### Dashboard
- Template statistics (total, by type, by cloud provider, by format)
- Quick action buttons
- System information panel

### Template Management
- List all templates with search
- Create new templates with full specification
- Update existing templates
- Delete templates
- Validate templates before creation

### Infrastructure Management
- Provision Azure/AWS resources
- View resource group status
- Estimate costs
- Delete resource groups

## API Integration

The Admin Console integrates with the Admin API at `http://localhost:7002`:

- **GET** `/api/admin/templates` - List templates
- **POST** `/api/admin/templates` - Create template
- **PUT** `/api/admin/templates/{id}` - Update template
- **DELETE** `/api/admin/templates/{id}` - Delete template
- **POST** `/api/admin/templates/validate` - Validate template
- **GET** `/api/admin/templates/stats` - Get statistics
- **POST** `/api/admin/infrastructure/provision` - Provision infrastructure

## Environment Variables

Create a `.env` file in `ClientApp/`:

```bash
REACT_APP_ADMIN_API_URL=http://localhost:7002
```

## Build for Production

```bash
cd ClientApp
npm run build
```

The optimized production build will be created in `ClientApp/build/`.

## Technology Stack

- **Frontend**: React 18 + TypeScript
- **HTTP Client**: Axios
- **Routing**: React Router v6
- **Backend**: ASP.NET Core 9
- **API**: RESTful Admin API (port 7002)

## Related Services

- **Admin API**: http://localhost:7002 (Swagger UI)
- **Platform API**: http://localhost:7001
- **Chat App**: http://localhost:7000

## Development

### File Structure

```
ClientApp/
├── public/
│   └── index.html
├── src/
│   ├── components/
│   │   ├── Dashboard.tsx
│   │   ├── TemplateList.tsx
│   │   ├── CreateTemplate.tsx
│   │   ├── TemplateDetails.tsx
│   │   └── InfrastructureManagement.tsx
│   ├── services/
│   │   └── adminApi.ts
│   ├── App.tsx
│   ├── App.css
│   ├── index.tsx
│   └── index.css
├── package.json
└── tsconfig.json
```

### Adding New Features

1. Create component in `src/components/`
2. Add route in `App.tsx`
3. Implement API calls in `services/adminApi.ts`
4. Style with CSS modules or inline styles

## Support

For issues or questions:
- Check Admin API Swagger UI: http://localhost:7002
- Review Admin API README: `../Platform.Engineering.Copilot.Admin/README.md`
- Check platform documentation: `/docs/`
