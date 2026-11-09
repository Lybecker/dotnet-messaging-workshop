#!/usr/bin/env bash
# One-time M4 setup: Application Insights, Service Bus and the trainer's Data Owner role.
set -euo pipefail

usage() {
    echo "Usage: $0 [--resource-group NAME] [--location REGION]"
}

resource_group="rg-m4-reliable-orders"
location="westeurope"
while [[ $# -gt 0 ]]; do
    case "$1" in
        --resource-group|--location)
            if [[ $# -lt 2 || -z "$2" || "$2" == -* ]]; then
                echo "Missing value for $1" >&2
                exit 2
            fi
            case "$1" in
                --resource-group) resource_group="$2" ;;
                --location) location="$2" ;;
            esac
            shift 2
            ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
done

for command in az dotnet python3; do
    if ! command -v "$command" >/dev/null 2>&1; then
        echo "Required command not found: $command" >&2
        exit 1
    fi
done

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
umask 077
outputs="$(mktemp)"
settings="$(mktemp "$script_dir/.demo.settings.XXXXXX")"
trap 'rm -f -- "$outputs" "$settings"' EXIT

echo "1/5  Checking your Azure sign-in..."
az account show --query '{subscription:name, user:user.name}' --output table
principal_id="$(az ad signed-in-user show --query id --output tsv)"
if [[ -z "$principal_id" ]]; then
    echo "Azure returned no signed-in user ID. Run az login with your trainer account." >&2
    exit 1
fi

echo "2/5  Resource group $resource_group in $location..."
az group create --name "$resource_group" --location "$location" --output none

echo "3/5  Deploying Application Insights, Service Bus and your Data Owner role..."
az deployment group create --resource-group "$resource_group" \
    --template-file "$script_dir/infra/main.bicep" \
    --parameters "trainerPrincipalId=$principal_id" \
    --query properties.outputs --output json > "$outputs"

echo "4/5  Writing demo.settings.json (read by both processes)..."
python3 - "$outputs" "$settings" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as source:
    outputs = json.load(source)
connection = outputs["appInsightsConnectionString"]["value"]
namespace = outputs["serviceBusNamespace"]["value"]
portal = outputs["appInsightsPortalUrl"]["value"]
if not all(isinstance(value, str) and value.strip() for value in (connection, namespace, portal)):
    raise ValueError("Azure deployment outputs must contain non-empty connection string, namespace and portal URL")
with open(sys.argv[2], "w", encoding="utf-8") as destination:
    json.dump({
        "APPLICATIONINSIGHTS_CONNECTION_STRING": connection,
        "ServiceBus": {"Namespace": namespace},
    }, destination, indent=2)
    destination.write("\n")
print("  Application Insights:", portal)
print("  Service Bus:", namespace)
PY
mv -- "$settings" "$script_dir/demo.settings.json"

echo "5/5  Building and waiting 60 seconds for the role assignment to propagate..."
dotnet build "$script_dir/M4.Orders/M4.Orders.csproj" -c Release --nologo -v q
dotnet build "$script_dir/M4.Payments/M4.Payments.csproj" -c Release --nologo -v q
sleep 60

echo "Done. Start Docker services from infra/ with docker compose up -d."
printf 'Then run: bash %q asb\n' "$script_dir/run-demo.sh"
echo "Follow the two terminal commands it prints, then press w in orders-api for warm-up traffic."
