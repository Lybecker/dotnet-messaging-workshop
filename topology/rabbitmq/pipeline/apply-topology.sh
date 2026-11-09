#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# Apply ONE team's RabbitMQ topology file to a broker, from the pipeline.
# Works the same for self-hosted (VMs / Docker) and managed (e.g. CloudAMQP),
# because it only talks to the management HTTP API through rabbitmqadmin v2.
#
#   apply-topology.sh <node-alias> <team>.rabbitmq.json [last-deployed-git-ref]
#
#   node-alias          section in the rabbitmqadmin config file, e.g. [test] or [prod].
#                       The pipeline writes it from the vault before calling this script:
#                         rabbitmqadmin config_file add_node --node prod --host <host> --port 443 \
#                           --username pipeline --password "$RMQ_PIPELINE_PASSWORD" --use-tls
#   last-deployed-ref   Git ref of the last successful deployment to this environment
#                       (the pipeline moves a tag, e.g. rabbitmq/prod, after each deploy).
#                       Anything in that version of the file but not in the current one is DELETED.
#                       Git is the state: no extra tool, no state file.
#
# Declares are idempotent: running the script twice changes nothing.
# Needs: bash, jq, git, rabbitmqadmin v2. The pipeline user needs configure rights
# (control plane) on the vhost; application users never do.
# Teaching sample: test against your RabbitMQ and rabbitmqadmin versions before use.
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

NODE=${1:?node alias}; FILE=${2:?topology file}; PREV=${3:-}
VHOST=$(jq -r .vhost "$FILE"); OWNER=$(jq -r .owner "$FILE")
rmq() { rabbitmqadmin --node "$NODE" --vhost "$VHOST" "$@"; }

# ── 1. Ownership: a team only declares names that start with its own prefix ──
bad=$(jq -r --arg o "$OWNER." '[.exchanges[], .queues[].name] | map(select(startswith($o) | not)) | .[]' "$FILE")
if [[ -n "$bad" ]]; then echo "Not owned by '$OWNER': $bad" >&2; exit 1; fi

# ── 2. Exchanges (publisher side) ────────────────────────────────────────────
for x in $(jq -r '.exchanges[]' "$FILE"); do
  rmq exchanges declare --name "$x" --type topic --durable true
done

# ── 3. Queues (consumer side): queue + its DLQ + a policy with the defaults + bindings ──
# Defaults live in a POLICY, not in queue arguments: a policy can be changed later,
# queue arguments cannot without deleting and recreating the queue.
for q in $(jq -r '.queues[].name' "$FILE"); do
  dlq="$q.dlq"
  rmq queues declare --name "$q"   --type quorum --durable true
  rmq queues declare --name "$dlq" --type quorum --durable true
  def=$(jq -c --arg dlq "$dlq" '.defaults + {"dead-letter-exchange": "", "dead-letter-routing-key": $dlq}' "$FILE")
  rmq policies declare --name "$q" --pattern "^${q//./\\.}\$" --apply-to queues --priority 10 --definition "$def"
  while IFS=$'\t' read -r ex rk; do
    rmq bindings declare --source "$ex" --destination-type queue --destination "$q" --routing-key "$rk"
  done < <(jq -r --arg q "$q" '.queues[] | select(.name == $q) | .bindings[] | [.exchange, .routingKey] | @tsv' "$FILE")
done

# ── 4. Prune: delete what was removed from the file since the last deployment ──
[[ -z "$PREV" ]] && { echo "No last-deployed ref given: nothing pruned."; exit 0; }
old=$(git show "$PREV:$FILE" 2>/dev/null || echo '{"exchanges":[],"queues":[]}')

removed() { comm -23 <(echo "$old" | jq -r "$1" | sort) <(jq -r "$1" "$FILE" | sort); }

# Bindings removed from a queue that still exists (or whose queue is removed below anyway)
while IFS=$'\t' read -r ex q rk; do
  [[ -z "$ex" ]] && continue
  echo "Unbinding $q from $ex"
  rmq bindings delete --source "$ex" --destination-type queue --destination "$q" --routing-key "$rk"
done < <(removed '.queues[] | .name as $q | .bindings[] | [.exchange, $q, .routingKey] | @tsv')

# Queues: the queue, its DLQ and its policy. Messages in them are lost: drain first
# (the pull request diff and the prod approval show the deletion before it happens).
for q in $(removed '.queues[].name'); do
  echo "Deleting queue $q (+ $q.dlq, policy)"
  rmq queues delete --name "$q"     --idempotently
  rmq queues delete --name "$q.dlq" --idempotently
  rmq policies delete --name "$q" || true
done

# Exchanges: only after every consumer has removed its bindings (expand / migrate / contract)
for x in $(removed '.exchanges[]'); do
  echo "Deleting exchange $x"
  rmq exchanges delete --name "$x" --idempotently
done
