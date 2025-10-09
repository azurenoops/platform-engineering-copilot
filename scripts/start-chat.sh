#!/bin/bash

echo "🚀 Starting Platform Chat App..."

# Get the directory where the script is located
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$SCRIPT_DIR/.."

echo "📦 Starting Chat Client on port 5001..."
cd "$PROJECT_ROOT/src/Platform.Engineering.Copilot.Chat.App/ClientApp"

# Check if node_modules exists
if [ ! -d "node_modules" ]; then
    echo "📥 Installing dependencies..."
    npm install
fi

# Start the client
npm run start
