# RabbitMQ topology as code

Same rule as Service Bus: the **publisher owns the exchange**, the **consumer owns its queue, DLQ,
policy and binding**, the **pipeline holds the configure rights**, and apps only get write/read.

| Where RabbitMQ runs | Broker created by | Topology created by | Removes what you delete? |
|---|---|---|---|
| Self-hosted on Kubernetes | RabbitMQ Cluster Operator | Messaging Topology Operator (`kubernetes/`), via GitOps | Yes |
| Self-hosted on VMs / Docker | your VM / compose tooling | Pipeline script with rabbitmqadmin v2 (`pipeline/`) | Yes (Git is the state) |
| Managed: CloudAMQP (Azure Marketplace, Azure regions) | CloudAMQP portal or its REST API | Same pipeline script, or Topology Operator via `connectionSecret` | Yes |
| Managed: Amazon MQ for RabbitMQ (AWS) | AWS (CloudFormation / CDK) | Same pipeline script | Yes |
| Local dev / docker-compose | compose | `definitions/*.json` loaded at boot | **No** (additive only) |

There is no first-party Azure RabbitMQ service.

## Without Kubernetes: `pipeline/apply-topology.sh`

One small JSON file per team (`pipeline/examples/*.rabbitmq.json`) describes what the team owns.
The pipeline runs, per team file:

```bash
rabbitmqadmin config_file add_node --node prod --host <host> --port 443 \
  --username pipeline --password "$RMQ_PIPELINE_PASSWORD" --use-tls      # from the vault
./apply-topology.sh prod teams/billing/billing.rabbitmq.json rabbitmq/prod  # last deployed tag
git tag -f rabbitmq/prod && git push -f origin rabbitmq/prod                 # after success
```

* **Declares** exchanges, quorum queues, a `.dlq` per queue, a policy with the defaults and the bindings.
  All declares are idempotent.
* **Deletes** what was removed from the file since the last deployed commit: Git is the state, so there is
  no state file to store or lock.
* **Refuses** names that do not start with the team's prefix (`billing.` for the billing file).
* Order across teams: publishers' exchanges first, then consumers' queues and bindings.

Defaults are explicit in a **policy** (`dead-letter-exchange`, `delivery-limit`, `message-ttl`) rather than
queue x-arguments, because policies can be changed later without recreating the queue. Only the queue
type (`quorum`) has to be an argument.

## On Kubernetes: `kubernetes/billing-topology.yaml`

The Messaging Topology Operator turns Queue, Exchange, Binding, Policy and Permission objects into
broker objects; delete the YAML and the object is deleted. With a `connectionSecret` it can also manage
a broker it did not create (VMs, CloudAMQP), so one format can cover every broker if you run Kubernetes.

## Permissions

Per user per vhost there are three regexes:

| Permission | Plane | Pipeline user | App user (`billing`) |
|---|---|---|---|
| configure (declare/delete) | control | `.*` (+ `policymaker` tag for policies) | `^$` |
| write (publish) | data | as needed | `^billing\.` |
| read (consume) | data | as needed | `^billing\.` |

```bash
rabbitmqadmin --node prod --vhost contoso permissions declare --username billing \
  --configure '^$' --write '^billing\.' --read '^billing\.'
```

Teaching samples: verify command names against your rabbitmqadmin v2 and RabbitMQ versions before use.
