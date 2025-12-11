#!/bin/bash
# upload-build.sh - Upload new build to server

VERSION=$1
SERVER_HOST=$2
SERVER_USER=${3:-nighthunt}
REMOTE_PATH="/opt/nighthunt/versions"

if [ -z "$VERSION" ] || [ -z "$SERVER_HOST" ]; then
    echo "Usage: $0 <version> <server_host> [user]"
    echo "Example: $0 v1.0.0 server.example.com"
    exit 1
fi

# Find build directory (adjust path as needed)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BUILD_DIR="$PROJECT_ROOT/Builds/HeadlessServer"

if [ ! -d "$BUILD_DIR" ]; then
    echo "Error: Build directory not found: $BUILD_DIR"
    echo "Please build the headless server first"
    exit 1
fi

VERSION_DIR="$REMOTE_PATH/$VERSION"

echo "Uploading version $VERSION to $SERVER_HOST..."
echo "Build directory: $BUILD_DIR"
echo "Remote path: $VERSION_DIR"

# Create version directory on server
ssh $SERVER_USER@$SERVER_HOST "mkdir -p $VERSION_DIR"

# Upload files
rsync -avz --progress \
    $BUILD_DIR/ \
    $SERVER_USER@$SERVER_HOST:$VERSION_DIR/

if [ $? -eq 0 ]; then
    echo ""
    echo "Upload completed successfully!"
    echo ""
    echo "To deploy this version, run on server:"
    echo "  ssh $SERVER_USER@$SERVER_HOST 'cd /opt/nighthunt/scripts && ./update-server.sh $VERSION'"
else
    echo "Error: Upload failed"
    exit 1
fi

