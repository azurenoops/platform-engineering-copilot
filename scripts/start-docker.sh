#!/bin/bash

# =============================================================================
# Platform Engineering Copilot - Docker Startup Script
# =============================================================================
# This script provides easy management of Docker containers for local development
#
# Usage:
#   ./scripts/start-docker.sh [mode] [options]
#
# Modes:
#   essentials  - Start only MCP Server + SQL Server (minimal setup)
#   full        - Start all services (MCP, Chat, Admin API, Admin Client, SQL)
#   dev         - Start with development settings (hot reload)
#   prod        - Start with production settings
#
# Options:
#   --with-proxy    - Include Nginx reverse proxy
#   --with-cache    - Include Redis cache
#   --rebuild       - Force rebuild of containers
#   --logs          - Follow logs after starting
#   --clean         - Remove volumes and do fresh start
#
# Examples:
#   ./scripts/start-docker.sh essentials
#   ./scripts/start-docker.sh full --with-proxy
#   ./scripts/start-docker.sh dev --logs
#   ./scripts/start-docker.sh full --rebuild --clean
# =============================================================================

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# =============================================================================
# Helper Functions
# =============================================================================

print_header() {
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}"
}

print_success() {
    echo -e "${GREEN}✅ $1${NC}"
}

print_error() {
    echo -e "${RED}❌ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ️  $1${NC}"
}

show_usage() {
    cat << EOF
${BLUE}Platform Engineering Copilot - Docker Startup Script${NC}

${GREEN}Usage:${NC}
  ./scripts/start-docker.sh [mode] [options]

${GREEN}Modes:${NC}
  ${YELLOW}essentials${NC}  - Start only MCP Server + SQL Server (minimal setup)
  ${YELLOW}full${NC}        - Start all services (MCP, Chat, Admin API, Admin Client, SQL)
  ${YELLOW}dev${NC}         - Start with development settings (hot reload)
  ${YELLOW}prod${NC}        - Start with production settings

${GREEN}Options:${NC}
  ${YELLOW}--with-proxy${NC}    - Include Nginx reverse proxy
  ${YELLOW}--with-cache${NC}    - Include Redis cache
  ${YELLOW}--rebuild${NC}       - Force rebuild of containers
  ${YELLOW}--logs${NC}          - Follow logs after starting
  ${YELLOW}--clean${NC}         - Remove volumes and do fresh start
  ${YELLOW}--help${NC}          - Show this help message

${GREEN}Examples:${NC}
  ./scripts/start-docker.sh essentials
  ./scripts/start-docker.sh full --with-proxy
  ./scripts/start-docker.sh dev --logs
  ./scripts/start-docker.sh full --rebuild --clean

${GREEN}Service Ports:${NC}
  ${YELLOW}MCP Server:${NC}      http://localhost:5100
  ${YELLOW}Chat App:${NC}        http://localhost:5001
  ${YELLOW}Admin API:${NC}       http://localhost:5050
  ${YELLOW}Admin Client:${NC}    http://localhost:5000
  ${YELLOW}SQL Server:${NC}      localhost:1433
  ${YELLOW}Redis:${NC}           localhost:6379
  ${YELLOW}Nginx:${NC}           http://localhost (80/443)

EOF
}

check_prerequisites() {
    print_header "Checking Prerequisites"
    
    # Check Docker
    if ! command -v docker &> /dev/null; then
        print_error "Docker is not installed. Please install Docker Desktop."
        exit 1
    fi
    print_success "Docker is installed"
    
    # Check Docker Compose
    if ! docker compose version &> /dev/null; then
        print_error "Docker Compose is not available. Please update Docker Desktop."
        exit 1
    fi
    print_success "Docker Compose is available"
    
    # Check if Docker daemon is running
    if ! docker ps &> /dev/null; then
        print_error "Docker daemon is not running. Please start Docker Desktop."
        exit 1
    fi
    print_success "Docker daemon is running"
    
    # Check for .env file
    if [ ! -f "$PROJECT_ROOT/.env" ]; then
        print_warning ".env file not found"
        if [ -f "$PROJECT_ROOT/.env.example" ]; then
            print_info "Copying .env.example to .env"
            cp "$PROJECT_ROOT/.env.example" "$PROJECT_ROOT/.env"
            print_warning "Please edit .env file with your configuration before continuing"
            print_info "Edit: $PROJECT_ROOT/.env"
            exit 1
        else
            print_error ".env.example not found. Please create .env file manually"
            exit 1
        fi
    fi
    print_success ".env file exists"
    
    echo ""
}

# =============================================================================
# Main Script
# =============================================================================

# Default values
MODE="full"
COMPOSE_FILES=()
PROFILES=()
REBUILD=false
FOLLOW_LOGS=false
CLEAN=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        essentials|full|dev|prod)
            MODE=$1
            shift
            ;;
        --with-proxy)
            PROFILES+=("proxy")
            shift
            ;;
        --with-cache)
            PROFILES+=("cache")
            shift
            ;;
        --rebuild)
            REBUILD=true
            shift
            ;;
        --logs)
            FOLLOW_LOGS=true
            shift
            ;;
        --clean)
            CLEAN=true
            shift
            ;;
        --help|-h)
            show_usage
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Change to project root
cd "$PROJECT_ROOT"

# Check prerequisites
check_prerequisites

# Build compose file list based on mode
print_header "Configuring Docker Compose for mode: $MODE"

case $MODE in
    essentials)
        if [ -f "docker-compose.essentials.yml" ]; then
            COMPOSE_FILES+=("-f" "docker-compose.essentials.yml")
            print_info "Using essentials configuration (MCP + SQL only)"
        else
            print_error "docker-compose.essentials.yml not found"
            exit 1
        fi
        ;;
    full)
        COMPOSE_FILES+=("-f" "docker-compose.yml")
        print_info "Using full configuration (all services)"
        ;;
    dev)
        COMPOSE_FILES+=("-f" "docker-compose.yml")
        if [ -f "docker-compose.dev.yml" ]; then
            COMPOSE_FILES+=("-f" "docker-compose.dev.yml")
            print_info "Using development overrides (hot reload enabled)"
        else
            print_warning "docker-compose.dev.yml not found, using base configuration only"
        fi
        ;;
    prod)
        COMPOSE_FILES+=("-f" "docker-compose.yml")
        if [ -f "docker-compose.prod.yml" ]; then
            COMPOSE_FILES+=("-f" "docker-compose.prod.yml")
            print_info "Using production overrides"
        else
            print_warning "docker-compose.prod.yml not found, using base configuration only"
        fi
        ;;
esac

# Build profile arguments
PROFILE_ARGS=()
if [ ${#PROFILES[@]} -gt 0 ]; then
    for profile in "${PROFILES[@]}"; do
        PROFILE_ARGS+=("--profile" "$profile")
        print_info "Enabling profile: $profile"
    done
fi

echo ""

# Clean volumes if requested
if [ "$CLEAN" = true ]; then
    print_header "Cleaning Up Volumes"
    print_warning "This will remove all data!"
    read -p "Are you sure? (y/N) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        docker compose "${COMPOSE_FILES[@]}" "${PROFILE_ARGS[@]}" down -v
        print_success "Volumes removed"
    else
        print_info "Clean cancelled, continuing without cleaning"
    fi
    echo ""
fi

# Stop any running containers
print_header "Stopping Existing Containers"
docker compose "${COMPOSE_FILES[@]}" "${PROFILE_ARGS[@]}" down
print_success "Existing containers stopped"
echo ""

# Build containers if rebuild flag is set
if [ "$REBUILD" = true ]; then
    print_header "Rebuilding Containers"
    docker compose "${COMPOSE_FILES[@]}" "${PROFILE_ARGS[@]}" build --no-cache
    print_success "Containers rebuilt"
    echo ""
fi

# Start containers
print_header "Starting Containers"
docker compose "${COMPOSE_FILES[@]}" "${PROFILE_ARGS[@]}" up -d
print_success "Containers started"
echo ""

# Wait a moment for services to initialize
print_info "Waiting for services to initialize..."
sleep 5

# Check service status
print_header "Service Status"
docker compose "${COMPOSE_FILES[@]}" "${PROFILE_ARGS[@]}" ps
echo ""

# Test health endpoints
print_header "Testing Health Endpoints"

test_endpoint() {
    local name=$1
    local url=$2
    if curl -sf "$url" > /dev/null 2>&1; then
        print_success "$name is healthy"
    else
        print_warning "$name is not responding yet (may still be starting)"
    fi
}

case $MODE in
    essentials)
        test_endpoint "MCP Server" "http://localhost:5100/health"
        ;;
    *)
        test_endpoint "MCP Server" "http://localhost:5100/health"
        test_endpoint "Chat App" "http://localhost:5001/health"
        test_endpoint "Admin API" "http://localhost:5050/health"
        test_endpoint "Admin Client" "http://localhost:5000/"
        ;;
esac

echo ""

# Print access information
print_header "🎉 Services Started Successfully!"

case $MODE in
    essentials)
        cat << EOF
${GREEN}MCP Server:${NC}      http://localhost:5100
${GREEN}SQL Server:${NC}      localhost:1433
${GREEN}Health Check:${NC}    http://localhost:5100/health

${YELLOW}Logs:${NC}
  docker compose ${COMPOSE_FILES[@]} logs -f platform-mcp

${YELLOW}Stop:${NC}
  docker compose ${COMPOSE_FILES[@]} down
EOF
        ;;
    *)
        cat << EOF
${GREEN}Services:${NC}
  ${YELLOW}MCP Server:${NC}      http://localhost:5100
  ${YELLOW}Chat App:${NC}        http://localhost:5001
  ${YELLOW}Admin API:${NC}       http://localhost:5050
  ${YELLOW}Admin Client:${NC}    http://localhost:5000
  ${YELLOW}SQL Server:${NC}      localhost:1433
EOF

        if [[ " ${PROFILES[@]} " =~ " cache " ]]; then
            echo -e "  ${YELLOW}Redis:${NC}           localhost:6379"
        fi
        
        if [[ " ${PROFILES[@]} " =~ " proxy " ]]; then
            echo -e "  ${YELLOW}Nginx:${NC}           http://localhost"
        fi

        cat << EOF

${YELLOW}Logs:${NC}
  docker compose ${COMPOSE_FILES[@]} logs -f

${YELLOW}Individual Service Logs:${NC}
  docker compose ${COMPOSE_FILES[@]} logs -f platform-mcp
  docker compose ${COMPOSE_FILES[@]} logs -f platform-chat
  docker compose ${COMPOSE_FILES[@]} logs -f admin-api
  docker compose ${COMPOSE_FILES[@]} logs -f admin-client

${YELLOW}Stop:${NC}
  docker compose ${COMPOSE_FILES[@]} down

${YELLOW}Stop and Remove Volumes:${NC}
  docker compose ${COMPOSE_FILES[@]} down -v
EOF
        ;;
esac

echo ""
print_header "Quick Commands"
cat << EOF
${YELLOW}View all containers:${NC}
  docker compose ${COMPOSE_FILES[@]} ps

${YELLOW}Restart a service:${NC}
  docker compose ${COMPOSE_FILES[@]} restart platform-mcp

${YELLOW}View resource usage:${NC}
  docker stats

${YELLOW}Execute commands in container:${NC}
  docker compose ${COMPOSE_FILES[@]} exec platform-mcp /bin/sh
EOF

echo ""

# Follow logs if requested
if [ "$FOLLOW_LOGS" = true ]; then
    print_header "Following Logs (Ctrl+C to exit)"
    docker compose "${COMPOSE_FILES[@]}" "${PROFILE_ARGS[@]}" logs -f
fi
