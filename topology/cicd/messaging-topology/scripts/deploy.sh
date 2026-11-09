#!/usr/bin/env bash
# Deploy (or preview) the whole shared bus for one environment, in dependency order.
#   deploy.sh <test|prod> [deploy|what-if|drift]
# Each file is its own deployment stack, so a team's stack only ever touches that team's entities,
# and an entity removed from a file is deleted on the next deploy.
set -euo pipefail
ENV=${1:?test or prod}; MODE=${2:-deploy}
RG="rg-messaging-$ENV"
shopt -s nullglob
drift=0

run() {   # run <stack-name> <bicep-file> <action-on-unmanage>
  local name=$1 file=$2 unmanage=$3 par="${2%.bicep}.$ENV.bicepparam"
  case $MODE in
    deploy)
      az stack group create --name "$name" --resource-group "$RG" \
        --template-file "$file" --parameters "$par" \
        --action-on-unmanage "$unmanage" --deny-settings-mode none --yes ;;
    what-if)   # posted to the pull request
      az deployment group what-if --resource-group "$RG" --template-file "$file" --parameters "$par" \
        --exclude-change-types Ignore NoChange ;;
    drift)     # scheduled: did anyone change the bus outside the pipeline?
      n=$(az deployment group what-if --resource-group "$RG" --template-file "$file" --parameters "$par" \
            --exclude-change-types Ignore NoChange --no-pretty-print --query "length(changes)" -o tsv)
      [[ "$n" == "0" ]] || { echo "::warning::Drift in $file ($n changes)"; drift=1; } ;;
  esac
}

# 1. Platform: the namespace. detachAll = removing it from the file never deletes the bus.
run topology-platform platform/namespace.bicep detachAll
# 2. Publishers: every topic exists before anyone subscribes to it.
for f in teams/*/topics.bicep;        do run "topology-$(basename "$(dirname "$f")")-topics" "$f" deleteResources; done
# 3. Consumers: subscriptions and queues.
for f in teams/*/subscriptions.bicep; do run "topology-$(basename "$(dirname "$f")")-subs"   "$f" deleteResources; done
for f in teams/*/queues.bicep;        do run "topology-$(basename "$(dirname "$f")")-queues" "$f" deleteResources; done
# 4. Applications deploy AFTER this pipeline, in their own pipelines, with data-plane rights only.

exit $drift
