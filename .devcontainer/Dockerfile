FROM mcr.microsoft.com/devcontainers/dotnet:10.0-noble

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates curl git jq openssl python3 shellcheck \
    && rm -rf /var/lib/apt/lists/*
