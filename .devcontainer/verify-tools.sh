#!/usr/bin/env bash
set -euo pipefail

for tool in dotnet az docker gh git bash python3 curl jq openssl shellcheck; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "Required tool is missing: $tool" >&2
        exit 1
    fi
done

dotnet --version
az version --output table
az bicep version
docker --version
docker compose version
gh --version
python3 --version
shellcheck --version

echo "Workshop tools are ready. Start the training services with:"
echo "  docker compose -f infra/docker-compose.yml up -d"
