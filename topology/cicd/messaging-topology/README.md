# messaging-topology: the shared bus in Git and CI/CD

A template repository for ONE shared Service Bus namespace per environment, used by many teams.

```
messaging-topology/
  platform/                      namespace (platform team), *.test / *.prod .bicepparam
  modules/                       topic / subscription / queue modules (shared, versioned)
  teams/
    orders/   topics.bicep         + topics.test|prod.bicepparam          (publisher)
    billing/  subscriptions.bicep  + subscriptions.test|prod.bicepparam   (consumer)
  scripts/check-names.sh         naming + ownership rules, runs on every PR
  scripts/deploy.sh              deploy | what-if | drift, in dependency order
  .github/CODEOWNERS             each team approves its own folder
  .github/workflows/topology.yml PR checks -> test -> approval -> prod, plus a nightly drift check
```

## How a change flows

1. **Pull request.** A team edits only its folder (add a line to subscribe, delete it to unsubscribe).
   The PR runs naming/ownership checks, Bicep build + lint, and posts a what-if for test.
   CODEOWNERS makes the owning team approve; platform approves platform/, modules/ and scripts/.
2. **Merge to main.** Deploys to **test**: one deployment stack per file, so a team's stack only touches
   that team's entities, and anything removed from a file is deleted.
3. **Approval.** The prod environment has required reviewers.
4. **Prod.** The same commit, the same templates; only the `.prod.bicepparam` files differ.

Order inside every deploy: platform (namespace) -> all topics -> all subscriptions/queues.
Applications deploy afterwards from their own pipelines and never create topology.

## Rights

| Identity | Plane | Rights |
|---|---|---|
| Pipeline (test / prod), OIDC | control | Contributor on the namespace's resource group + RBAC Administrator limited to the Data Sender / Receiver roles |
| Pipeline (`*-plan`), OIDC | read | Reader + what-if, for PR previews and the drift check |
| Applications | data | Data Sender / Data Receiver on their own entities only (assigned by the modules) |
| People | read | Reader in prod; changes go through a pull request |

## Things to know

* **Deleting a topic deletes every subscription on it**, including other teams'. Retire a topic with
  expand / migrate / contract: first every consumer removes its subscription, then the publisher removes the topic.
* **No CanNotDelete lock on the namespace**: locks are inherited by child resources and would stop the stacks
  from deleting removed subscriptions. Protect the namespace with RBAC (only the pipeline can write) and the
  platform stack's `detachAll`.
* Deny settings on the stacks (`--deny-settings-mode denyWriteAndDelete`) can block portal changes entirely;
  start with `none` + PR review + the drift check, tighten once the flow is stable.
* Alternative layout: topology next to each service's code, deployed by that service's pipeline. It works,
  but the cross-team checks (naming, ownership, ordering, drift) are harder to enforce. With one company and
  one shared bus, a single topology repo is simpler.
* **RabbitMQ:** the same repo and stages work: `teams/<team>/<team>.rabbitmq.json` applied by
  `../rabbitmq/pipeline/apply-topology.sh` (Git is the state), or Topology Operator YAML synced by
  Flux / Argo CD on Kubernetes.

Teaching sample: not tested against a live subscription. Verify CLI flags and API versions before use.
