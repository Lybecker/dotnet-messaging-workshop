#!/usr/bin/env bash
# Deletes the resource group created by setup.sh; Azure performs deletion asynchronously.
set -euo pipefail

usage() {
    echo "Usage: $0 [--resource-group NAME]"
}

resource_group="rg-m4-reliable-orders"
while [[ $# -gt 0 ]]; do
    case "$1" in
        --resource-group)
            if [[ $# -lt 2 || -z "$2" || "$2" == -* ]]; then
                echo "Missing value for $1" >&2
                exit 2
            fi
            resource_group="$2"
            shift 2
            ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
done

if ! command -v az >/dev/null 2>&1; then
    echo "Required command not found: az" >&2
    exit 1
fi
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
az group delete --name "$resource_group" --yes --no-wait
rm -f -- "$script_dir/demo.settings.json"
echo "Deletion requested for $resource_group. Azure is deleting it in the background."
