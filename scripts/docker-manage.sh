#!/bin/bash

# Docker management script for Platform Supervisor
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
PROJECT_NAME="platform-supervisor"
COMPOSE_FILES="-f docker-compose.yml"

# Helper functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if Docker is running
check_docker() {
    if ! docker info > /dev/null 2>&1; then
        log_error "Docker is not running. Please start Docker and try again."
        exit 1
    fi
}

# Environment setup
setup_env() {
    local env_type=${1:-dev}
    
    case $env_type in
        dev|development)
            COMPOSE_FILES="-f docker-compose.yml -f docker-compose.dev.yml"
            log_info "Setting up development environment"
            ;;
        prod|production)
            COMPOSE_FILES="-f docker-compose.yml -f docker-compose.prod.yml"
            log_info "Setting up production environment"
            ;;
        *)
            log_info "Setting up default environment"
            ;;
    esac
}

# Build images
build() {
    log_info "Building Docker images..."
    docker-compose $COMPOSE_FILES build --parallel
    log_success "Build completed"
}

# Start services
start() {
    local env_type=${1:-dev}
    setup_env $env_type
    
    log_info "Starting Platform Supervisor services..."
    docker-compose $COMPOSE_FILES up -d
    
    log_info "Waiting for services to be healthy..."
    sleep 10
    
    # Check service health
    check_health
}

# Stop services
stop() {
    log_info "Stopping Platform Supervisor services..."
    docker-compose $COMPOSE_FILES down
    log_success "Services stopped"
}

# Restart services
restart() {
    local env_type=${1:-dev}
    stop
    start $env_type
}

# Check service health
check_health() {
    log_info "Checking service health..."
    
    # Check Platform API
    if curl -f http://localhost:5000/health > /dev/null 2>&1; then
        log_success "Platform API is healthy"
    else
        log_warning "Platform API health check failed"
    fi
    
    # Check SQL Server
    if docker-compose $COMPOSE_FILES exec -T sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P SupervisorDB123! -Q "SELECT 1" > /dev/null 2>&1; then
        log_success "SQL Server is healthy"
    else
        log_warning "SQL Server health check failed"
    fi
    
    # Check MCP Server
    if docker-compose $COMPOSE_FILES exec -T mcp-server netstat -an | grep 8080 | grep LISTEN > /dev/null 2>&1; then
        log_success "MCP Server is healthy"
    else
        log_warning "MCP Server health check failed"
    fi
}

# View logs
logs() {
    local service=${1:-}
    if [ -n "$service" ]; then
        log_info "Showing logs for $service..."
        docker-compose $COMPOSE_FILES logs -f $service
    else
        log_info "Showing logs for all services..."
        docker-compose $COMPOSE_FILES logs -f
    fi
}

# Clean up
clean() {
    log_info "Cleaning up Docker resources..."
    docker-compose $COMPOSE_FILES down -v --remove-orphans
    docker system prune -f
    log_success "Cleanup completed"
}

# Database operations
db_migrate() {
    log_info "Running database migrations..."
    docker-compose $COMPOSE_FILES exec platform-api dotnet ef database update
    log_success "Database migrations completed"
}

db_seed() {
    log_info "Seeding database with initial data..."
    docker-compose $COMPOSE_FILES exec platform-api dotnet run --project src/Platform.Engineering.Copilot.Data/Platform.Engineering.Copilot.Data.csproj
    log_success "Database seeding completed"
}

# Show status
status() {
    log_info "Platform Supervisor Service Status:"
    docker-compose $COMPOSE_FILES ps
    echo
    log_info "Port mappings:"
    echo "Platform API: http://localhost:5000"
    echo "MCP Server: http://localhost:8080"
    echo "SQL Server: localhost:1433"
    echo "Adminer (dev): http://localhost:8081"
    echo "Nginx (with proxy profile): http://localhost:80"
}

# Show help
show_help() {
    echo "Platform Supervisor Docker Management Script"
    echo
    echo "Usage: $0 [COMMAND] [OPTIONS]"
    echo
    echo "Commands:"
    echo "  build               Build Docker images"
    echo "  start [env]         Start services (env: dev|prod, default: dev)"
    echo "  stop                Stop services"
    echo "  restart [env]       Restart services"
    echo "  logs [service]      Show logs (optionally for specific service)"
    echo "  status              Show service status"
    echo "  health              Check service health"
    echo "  clean               Clean up Docker resources"
    echo "  db:migrate          Run database migrations"
    echo "  db:seed             Seed database with initial data"
    echo "  help                Show this help message"
    echo
    echo "Examples:"
    echo "  $0 start dev        Start in development mode"
    echo "  $0 start prod       Start in production mode"
    echo "  $0 logs platform-api Show logs for platform API"
    echo "  $0 db:migrate       Run database migrations"
}

# Main script logic
check_docker

case "${1:-help}" in
    build)
        build
        ;;
    start)
        start ${2:-dev}
        ;;
    stop)
        stop
        ;;
    restart)
        restart ${2:-dev}
        ;;
    logs)
        logs $2
        ;;
    status)
        status
        ;;
    health)
        check_health
        ;;
    clean)
        clean
        ;;
    db:migrate)
        db_migrate
        ;;
    db:seed)
        db_seed
        ;;
    help|--help|-h)
        show_help
        ;;
    *)
        log_error "Unknown command: $1"
        show_help
        exit 1
        ;;
esac