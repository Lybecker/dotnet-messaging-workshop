# M4 · Reliable orders – one run, two uses

**Rebus, two processes, everything from the Reliability section – and every step lands in Application Insights.**

For a short live-demo checklist with restart instructions and an example transaction
screenshot, use the [presenter guide](./PRESENTER-GUIDE.md).

* **Slide 41 (Reliability):** run it live, about 12 minutes.
* **Slide 49 (observability):** no second run. Open Application Insights and walk through the orders you placed at slide 41, about 10 minutes.

```
                ChargePayment (send, outbox)                      PaymentCharged / PaymentFailed (publish, outbox)
 POST /orders ──► orders-api ──► [ m4-payments ] ──► payments-worker ──► "card gateway"
   (keys)           │  ▲                                  │   inbox · retry tiers · fail fast
                    │  └──────── events ◄─────────────────┘           │
               SQL: Orders + outbox                                   └─► [ m4-error ] ──(r)──► back, or m4-error-parked
```

## Files

| File | What |
|---|---|
| `M4.Orders/Program.cs` | The flow of orders-api: Rebus with routing, outbox and subscriptions; the keys |
| `M4.Orders/PlaceOrder.cs` | POST /orders: order row + ChargePayment in **one** SQL transaction (outbox); the duplicate and the crash |
| `M4.Orders/OrderHandlers.cs` | Pending → Paid / Failed when the events arrive (eventual consistency) |
| `M4.Orders/Keyboard.cs` · `Redrive.cs` | The demo keys · the error-queue runbook as code |
| `M4.Payments/Program.cs` | The flow of payments-worker: retry tiers, fail fast, outbox |
| `M4.Payments/ChargePaymentHandler.cs` | Inbox + payment row + PaymentCharged in one transaction; good vs bad log line |
| `M4.Payments/ChargePaymentFailedHandler.cs` | Tier 2: permanent → PaymentFailed + error queue; transient → delayed retry with back-off |
| `M4.Payments/PaymentGateway.cs` | The fake gateway: `alice` ok, `flaky` times out once, `declined` permanent, `slow` 15 s |
| `M4.Shared/Transport.cs` | The broker switch (broker.json): the only Service Bus vs RabbitMQ differences |
| `M4.Shared/Telemetry.cs` | Application Insights: **the three lines** |
| `M4.Shared/TelemetryPresentation.cs` | Plumbing: successful background poll filtering, readable Rebus spans and Service Bus SDK tracing |
| `setup.sh` · `run-demo.sh` · `teardown.sh` | Azure resources · print commands / run one process per terminal · delete everything |
| `queries.kql` | Six ready queries for the Logs blade |

Prerequisites: Bash 3.2+, Python 3 (`python3`), Azure CLI (`az login`), .NET 10 SDK,
Docker (SQL Server), rights to create resources and role assignments. Use Bash on
macOS/Linux, Git Bash on Windows, or WSL with the tools installed inside WSL and
Docker Desktop's WSL integration enabled. Run the following from this directory.

## Bash quick start

From the repository root, prepare the demo once:

```bash
cd src/M4.ReliableOrders
./setup.sh
docker compose -f ../../infra/docker-compose.yml up -d
./run-demo.sh asb
```

The last command prints instructions; it does not open terminals or start processes.
Open two Bash terminals at the repository root and run:

**Terminal 1 — payments-worker**

```bash
cd src/M4.ReliableOrders
./run-demo.sh asb --role payments
```

**Terminal 2 — orders-api**

```bash
cd src/M4.ReliableOrders
./run-demo.sh asb --role orders
```

Use `rabbit` instead of `asb` in both terminals to switch brokers. After a crash
scenario, restart the affected process with the same role command.
The local launcher defaults `AZURE_TOKEN_CREDENTIALS` to `AzureCliCredential`, using
your `az login` rather than probing Azure managed identity on your laptop. An
explicitly set `AZURE_TOKEN_CREDENTIALS` is preserved.

## The day before (20 min)

1. `./setup.sh` (about 5 minutes): Application Insights + Log Analytics + a Service Bus namespace (Standard, Entra ID only).
   It writes `demo.settings.json`, which both processes read. Note the Application Insights link it prints.
   Optional arguments: `--resource-group NAME --location REGION`.
2. In another terminal, run `docker compose -f ../../infra/docker-compose.yml up -d`
   (SQL Server; RabbitMQ if you want to show the broker switch).
3. `./run-demo.sh asb` prints two commands. Run each in its own Bash terminal,
   payments first, then orders. Each command builds its project in Release and runs it:
   `./run-demo.sh asb --role payments` and `./run-demo.sh asb --role orders`.
   Press **w** in the orders-api window (two minutes of mixed traffic) and run every key once.
4. Check in the portal: Application Map shows **orders-api** and **payments-worker** connected; Transaction search finds an
   order id; Failures shows `BusinessRuleViolationException` and `TimeoutException`.
5. Save `queries.kql` in Logs (query pack or a pinned tab). Take screenshots as a fallback (venue blocks AMQP port 5671?).

## Slide 41 – run it (about 12 min)

Two windows side by side: **orders-api** (you type here) and **payments-worker**. Start with `./run-demo.sh asb`
(or `rabbit`) and run its printed commands. Omitting the broker uses `broker.json` / `BROKER`.
Press **w** first: the warm-up traffic fills Application Insights while you talk.
The key guide is printed at startup and again after each command finishes
(after the two-minute traffic loop for **w**). Orders are asynchronous: the guide
can reappear while payments and retries are still running. **q** exits; **x**
deliberately crashes Orders, so restart it to see the guide again.

| Key | Min | What happens | Say |
|---|---|---|---|
| ENTER | 1 | `alice`: order *Pending* → ChargePayment → charged → PaymentCharged → order *Paid* (press **s**) | "A command to one owner, an event to anyone. Pending first: eventual consistency." |
| **x** | 2 | orders-api commits the order and dies before the outbox sends. Start it again (same command): the command is sent, the order becomes *Paid* | "The dual-write problem. The command was in the database, in the same transaction as the order." |
| **d** | 1 | The same ChargePayment twice, same MessageId. payments-worker: *Duplicate message … skipped* | "At-least-once means duplicates. The inbox row commits with the payment, so the second one is a no-op." |
| **k** | 2 | `slow`: payments-worker says *close this window now*. Close it, start it again: the message comes back (RabbitMQ at once, Service Bus after the 30 s lock) and is charged once | "Nothing is lost when a consumer dies: no ack, no delete. And nothing half-done: one transaction." |
| **t** | 2 | `flaky`: 3 immediate attempts fail → tier 2 → *delayed retry 1* about 5 s later → charged | "Will a retry help? Yes, later. Immediate retries for blips, delayed retries for outages." |
| **f** | 2 | `declined`: fail fast, no retries → PaymentFailed → order *Failed*; the message is in **m4-error** | "A declined card is a business outcome, not an outage. Retrying would be wrong." |
| **r** | 1 | Re-drive: transient failures go back to m4-payments, the declined one goes to m4-error-parked | "Every error queue needs an owner and a runbook. This is the runbook, as code." |
| — | 1 | Optional: change `broker.json`, restart both, press **t** again | "Same code, other broker." |

Show the code between keys: `M4.Payments/Program.cs` (retry tiers, fail fast, outbox) and `ChargePaymentHandler.cs`
(inbox + payment + event in one transaction).

## Slide 49 – the same run in Application Insights (about 10 min)

Tabs ready: Transaction search · Failures · Application Map · Logs (`queries.kql`).

For a clean run, start both processes, wait for their listening messages, then note
the current time **before** sending orders. Use that time as the start of a custom
time range in Application Insights. This excludes startup provisioning and historical
managed-identity failures. Keep both processes running except during the deliberate
crash scenarios; allow 1–3 minutes for telemetry ingestion.

1. **Transaction search** → the `alice` order id (`order.id`): POST /orders → send ChargePayment → payments-worker handles →
   publish PaymentCharged → orders-api handles. The gap between send and handle is the time in the queue. Open a log
   line: OrderId, MessageId, CorrelationId, Attempt are columns.
2. **The flaky order**: three failed attempts, then the delayed retry ~5 s later – all in one end-to-end view.
3. **Failures** → `BusinessRuleViolationException` ("Card declined") → sample → end-to-end: one attempt only (fail fast),
   then PaymentFailed. "Who owns m4-error? Who gets the alert?"
4. **Logs**, query 3: the duplicate the inbox skipped. Query 5: the bad log line has empty OrderId and Amount columns.
5. **Application Map**: orders-api ↔ payments-worker with call counts and failure rate. Nobody drew it: it comes from the
   trace context Rebus puts in the message headers (`EnableDiagnosticSources`).
6. Close with the habits: name every process, carry the trace in the message, log templates not strings, tag business IDs.

### What the cleaned-up telemetry shows

- Role names stay `orders-api` and `payments-worker`. Instance IDs are
  `<machine-name>:<role-name>`, stable across restarts on one machine and distinct
  between machines and roles. These IDs are for the local demo, not replica identities.
- Rebus operations have short names such as `ChargePayment send`, `ChargePayment receive`
  and `ChargePayment process`, with `messaging.system = rebus`. They are logical
  framework operations, not measurements of network delivery.
- Azure Service Bus SDK spans are collected separately via its experimental
  ActivitySource support. They describe actual SDK operations and supply the
  Azure-specific metadata used by Application Insights. RabbitMQ mode still works,
  but does not produce Azure Service Bus SDK spans. Exact portal rendering depends
  on the exporter and portal version.
- Successful, root-level SQL queries matching the pinned Rebus outbox polling
  query for `RebusOutboxOrders` / `RebusOutboxPayments` are not exported.
  Failed polls, business SQL, outbox writes, unrecognized queries and SQL within a
  business trace remain visible. Background cleanup may still appear.
- `localhost | Messaging` is still the local SQL database, not the broker.
  SQL has not been moved to Azure. Startup management calls can still appear as
  HTTP dependencies if you include startup in the selected time range.

Telemetry checks (from the repository root):

```bash
dotnet run --project tests/M4.Telemetry.Tests
# Optional: verify filtering against the running demo SQL database:
dotnet run --project tests/M4.Telemetry.Tests -- --live-sql
```

The optional SQL check requires the M4 tables created by starting the demo once.

## Troubleshooting

* **Nothing in Application Insights:** ingestion takes 1–3 minutes; check `demo.settings.json` exists next to this file.
* **401 from Service Bus:** the role assignment needs a minute or two after `setup.sh`.
* **Managed identity / IMDS errors on your laptop:** use the Bash role commands above
  and run `az login`. Remove an unintended `AZURE_TOKEN_CREDENTIALS` override with
  `unset AZURE_TOKEN_CREDENTIALS` before restarting.
* **SQL errors at start-up:** `docker compose up -d` in `infra/`; the tables are created on first start.
* **k on Service Bus waits:** the message returns when its lock expires (30 s). On RabbitMQ it returns at once.
* Run `dotnet build` and a full dry run before the day.

## Cleanup

Run `./teardown.sh` (or `./teardown.sh --resource-group NAME` if setup used a custom group).
This requests deletion of the entire resource group and removes local settings only
after Azure accepts the request. Azure deletion continues asynchronously.
