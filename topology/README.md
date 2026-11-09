# Topology: who creates queues, topics and subscriptions

* **Dev:** the framework may create topology at startup (fast feedback).
* **Test and prod (alike):** topology comes from code in the owning team's repo, deployed by the pipeline.
  The framework is told **not** to create or check anything, and the app only has data-plane rights.

| Folder | What |
|---|---|
| `frameworks/RebusTopology.cs` | Rebus: pinned topic + message type names, explicit defaults, provisioning only in dev |
| `frameworks/WolverineTopology.cs` | Wolverine: the same for Wolverine |
| `servicebus/` | Reusable Bicep modules (topic, subscription, queue) + deployment-stack command |
| `rabbitmq/` | Pipeline script (rabbitmqadmin v2), Topology Operator YAML and definitions JSON (self-hosted and managed) |
| `cicd/` | The shared bus in Git and CI/CD: repo layout, CODEOWNERS, naming check, GitHub Actions workflow, deploy script |

These files are teaching snippets: they are not part of the solution build. Compile and test
against your package versions before you use them.
