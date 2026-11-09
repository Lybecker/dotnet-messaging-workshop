#!/usr/bin/env bash
# Deploys the training environment and prints the environment variables for the samples.
# Usage: ./deploy.sh <resource-group> [location] [--with-rabbitmq]
set -euo pipefail
RG=${1:?resource group}; LOCATION=${2:-westeurope}; RABBIT=${3:-}

az group create -n "$RG" -l "$LOCATION" -o none

ME_ID=$(az ad signed-in-user show --query id -o tsv)
ME_UPN=$(az ad signed-in-user show --query userPrincipalName -o tsv)
MY_IP=$(curl -s https://api.ipify.org || echo "")

EXTRA=()
if [[ "$RABBIT" == "--with-rabbitmq" ]]; then
  RABBIT_PW=$(openssl rand -base64 18)
  EXTRA+=(deployRabbitMq=true rabbitMqPassword="$RABBIT_PW")
  echo "RabbitMQ password (store it safely): $RABBIT_PW"
fi

az deployment group create -g "$RG" -n training -f main.bicep \
  -p trainerPrincipalId="$ME_ID" trainerPrincipalName="$ME_UPN" clientIpAddress="$MY_IP" "${EXTRA[@]}" -o none

echo; echo "# --- export these before running the samples (Azure mode) ---"
az deployment group show -g "$RG" -n training --query properties.outputs -o json |
  python3 -c 'import json,sys; o=json.load(sys.stdin)
for k in ["ASB_NAMESPACE","SQL_CONNECTION","AZURE_CLIENT_ID"]: print(f"export {k}=\"{o[k][\"value\"]}\"")'
echo "# Locally: do NOT export AZURE_CLIENT_ID – DefaultAzureCredential then uses your az login."
echo "# In Azure (Container Apps / App Service / VM with the identity attached): export it."
echo; echo "Next: run sql-grant-identity.sql against the Messaging database as yourself (Entra admin)."
