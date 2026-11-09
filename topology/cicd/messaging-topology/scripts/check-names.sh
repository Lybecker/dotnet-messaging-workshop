#!/usr/bin/env bash
# Naming and ownership rules for the shared bus. Runs on every pull request; fails on any violation.
set -uo pipefail
fail=0
err() { echo "::error file=$1::$2"; fail=1; }

for dir in teams/*/; do
  dir=${dir%/}; team=$(basename "$dir")

  # 1. Topics: <domain>.<event>.v<major>, and only the team that owns <domain> may declare them.
  if [[ -f "$dir/topics.bicep" ]]; then
    while read -r t; do
      [[ "$t" =~ ^[a-z]+\.[a-z0-9-]+\.v[0-9]+$ ]] || err "$dir/topics.bicep" "Topic '$t' must look like <domain>.<event>.v<major>"
      [[ "$t" == "$team."* ]] || err "$dir/topics.bicep" "Topic '$t' is not owned by '$team'"
    done < <(grep -oE "^\s*'[^']+'\s*$" "$dir/topics.bicep" | tr -d " '")
  fi

  # 2. Subscriptions: the subscription name is the consuming team, nothing else.
  if [[ -f "$dir/subscriptions.bicep" ]]; then
    while read -r s; do
      [[ "$s" == "$team" ]] || err "$dir/subscriptions.bicep" "Subscription name '$s' must be '$team'"
    done < <(grep -oE "subscriptionName:\s*'[^']+'" "$dir/subscriptions.bicep" | sed -E "s/.*'([^']+)'/\1/")
  fi

  # 3. Teams declare entities, never namespaces.
  grep -lq "Microsoft.ServiceBus/namespaces@" "$dir"/*.bicep 2>/dev/null \
    && err "$dir" "Namespaces belong in platform/, not in a team folder"
done

# 4. Nobody hands out Azure Service Bus Data Owner (data-plane role with control-plane power).
grep -rl "090c5cfd-751d-490a-894a-3ce6f1109419" --include='*.bicep' . && err "." "Data Owner role found"

exit $fail
