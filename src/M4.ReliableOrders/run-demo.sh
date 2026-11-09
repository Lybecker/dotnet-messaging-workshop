#!/usr/bin/env bash
# Prints two terminal commands, or runs one interactive demo process with --role.
set -euo pipefail

usage() {
    echo "Usage: $0 [asb|rabbit] [--role payments|orders]"
}

broker=""
role=""
while [[ $# -gt 0 ]]; do
    case "$1" in
        asb|rabbit)
            if [[ -n "$broker" ]]; then
                echo "Specify only one broker." >&2
                exit 2
            fi
            broker="$1"
            shift
            ;;
        --role)
            if [[ $# -lt 2 || ( "$2" != payments && "$2" != orders ) ]]; then
                echo "--role must be payments or orders." >&2
                exit 2
            fi
            role="$2"
            shift 2
            ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown argument: $1" >&2; usage >&2; exit 2 ;;
    esac
done

if ! command -v dotnet >/dev/null 2>&1; then
    echo "Required command not found: dotnet" >&2
    exit 1
fi
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
if [[ ! -f "$script_dir/demo.settings.json" ]]; then
    echo "Warning: no demo.settings.json. Run setup.sh for Application Insights, or set APPLICATIONINSIGHTS_CONNECTION_STRING." >&2
fi

if [[ -n "$role" ]]; then
    # This launcher runs locally; do not probe Azure's managed-identity endpoint.
    export AZURE_TOKEN_CREDENTIALS="${AZURE_TOKEN_CREDENTIALS:-AzureCliCredential}"
    case "$role" in
        payments) project="M4.Payments" ;;
        orders) project="M4.Orders" ;;
    esac
    cd -- "$script_dir/$project"
    if [[ -n "$broker" ]]; then
        exec dotnet run --project "$project.csproj" -c Release -- "$broker"
    else
        exec dotnet run --project "$project.csproj" -c Release
    fi
fi

echo "Run each command in its own Bash terminal (payments first):"
for role in payments orders; do
    printf '  bash %q' "$script_dir/run-demo.sh"
    if [[ -n "$broker" ]]; then printf ' %q' "$broker"; fi
    printf ' --role %s\n' "$role"
done
echo "Each command builds and runs its process. No processes have been started yet."
echo "Use the orders-api terminal: ENTER, t, f, d, k, x, r, s, w."
echo "To restart after k or x, run the same role command again."
