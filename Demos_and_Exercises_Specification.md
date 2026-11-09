# Event- and Message-Driven .NET: Demos and Exercises Requirements

**What this document is.** These are the requirements for the C# demos and exercises used in the one-day training *Event- & Message-Driven Solutions in .NET: Patterns, Practices and Platform Choices*. It describes, for each demo and exercise:
- why it exists (intent)
- where it is used (slides)
- what it must do (behaviour)
- how the trainer and attendees use it, step by step
- how to check it is done (acceptance)

**What this document is not.** It does not tell you how to write the code or set up a machine. Any developer or AI agent can implement or fix the code in their own way, provided every requirement here holds.

**The most important requirement: the code and the slides must stay cohesive.** The deck shows code from the repository on many slides, and names files, folders, queues, keys and commands. Section 3 lists every place where a slide depends on the code. It also lists the rules for changing either side, and the mismatches that exist today. **Any fix or update to the code MUST keep Section 3 true.** If a fix has to change a line that a slide shows, update the slide in the same change.

Words such as **MUST**, **MUST NOT** and **SHOULD** are requirements.

---

## 1. Context

- **Audience:** .NET developers and architects at the customer.
- **Today:** they use **Azure Service Bus** through the native SDK (`Azure.Messaging.ServiceBus`).
- **Under evaluation:** **RabbitMQ** as an alternative broker, and a messaging framework (**Rebus** or **Wolverine**) instead of the native SDK.
- **Neutrality:** the broker comparison MUST stay neutral. No demo may favour one broker through how it is coded or configured.
- **Deck:** 82 slides, two of them hidden (slide 7 "Expected timing" and slide 73 "If you switch").
- **Demo marker:** a **green semicircle in the top-left corner** marks a slide where a demo is run. A red semicircle top-right marks an optional slide.

### 1.1 Slide map

| ID | Slide(s) | Section | Purpose |
|---|---|---|---|
| M1 | 17 (green dot) | Core principles | Temporal coupling: a synchronous call fails when the dependency is down; a queued call is accepted and completes later |
| Ex1 | 18 | Core principles | Paper exercise: command, event or query? |
| M2 / M3 | 28 (green dot), 29 | Messaging frameworks | Two processes, one command, one event, in Rebus (M2) and Wolverine (M3); switch broker with one setting |
| Ex2 | 30 | Messaging frameworks | Attendees wire Orders → Shipping with Rebus or Wolverine |
| M4 | 41 (green dot; run), 49 (walk-through) | Reliability, Observability | Outbox, inbox, retry tiers, fail fast, error queue and re-drive, at-least-once; the same run shown in Application Insights later |
| Ex3 | 42 | Reliability | Attendees make a flaky consumer reliable with Rebus or Wolverine |
| M5 | 56, 58 (green dot), 59 (green dot) | Sagas | The same order flow as orchestration and as choreography, with compensation |
| Ex4 | 69 | Operations & platform | Whiteboard: design your topology |
| Lab run sheet | 78 | Appendix | Commands, ports, module list |
| Azure (optional) | 79 | Appendix | Running the demos against Azure |

Green dots MUST be on exactly slides 17, 28, 41, 58 and 59. If a demo moves to another slide, move its dot too.

---

## 2. Global requirements

### 2.1 Teaching requirements
1. **Complexity follows the slides.** A demo or exercise MUST NOT use a concept the slides have not presented yet:
   - **M1:** no broker and no framework. An in-process queue stands in for the broker.
   - **M2, M3 and Ex2:** only two processes, one command (sent to the one queue that owns it), one event (published and subscribed) and the broker switch. They MUST NOT contain retry configuration, error queues, outbox/inbox, sagas, ordering, deferral or telemetry.
   - **M4 and Ex3:** the full Reliability section: at-least-once delivery, idempotency, outbox, retry tiers, transient vs permanent failures, error queue, re-drive. M4 also produces telemetry for Application Insights, which is shown later at slide 49.
   - **M5:** sagas, orchestration vs choreography, compensating actions.
2. **No ordering demos.** Message ordering (sessions, single active consumer, consistent-hash exchange) is taught on slides only. No sample code, queues or broker plugins for it.
3. **No runnable native-SDK demos.** Native `Azure.Messaging.ServiceBus` and `RabbitMQ.Client` code appears on slides only (Section 3.4). Every runnable demo uses Rebus or Wolverine, except M1, which uses no broker.
4. **No .NET Aspire** in any form. Observability uses OpenTelemetry and Application Insights.
5. **Both brokers, one setting.** Every framework demo and exercise MUST run on Azure Service Bus and on RabbitMQ:
   - the broker is selected by one setting, `broker.json` in the repository root, which a command-line argument (`asb` or `rabbit`) or the environment variable `BROKER` can override;
   - all broker-specific code of a project lives in exactly one file, which Section 3 names;
   - handlers, routing, Send/Publish/Subscribe and retry decisions MUST be identical on both brokers.

### 2.2 Code organisation requirements
1. Every runnable project's `Program.cs` starts with a **short description** of at most six comment lines, labelled **Purpose**, **Show** and **Run**, and **no slide numbers**. The rest of `Program.cs` is the overall flow only (host, menu or keys, start and stop). Everything else lives in other, well-named files.
2. Files the trainer never shows are marked as plumbing in a first-line comment.
3. Names follow slide order:
   - demos `M1.Fundamentals`, `M2.Rebus`, `M3.Wolverine`, `M4.ReliableOrders`, `M5.Sagas`;
   - exercises `Ex2.FirstEndpoint`, `Ex3.ReliableConsumer`, `Ex4.Topology` (Ex1 is slide-only);
   - exercise variants `Rebus.Starter`, `Rebus.Solution`, `Wolverine.Starter`, `Wolverine.Solution`.
4. All queue, topic and subscription names in this document are part of the contract with the slides and runbooks. Do not rename them.

### 2.3 Technical constraints (not set-up instructions)
- .NET 10 and C#. The full solution MUST restore and build with **0 errors**.
- Package versions are managed centrally and MUST be **exact versions**: floating versions such as `7.*` fail the restore with NU1011. Rebus packages must be mutually compatible, and so must the Wolverine packages.
- **Local environment** (these values appear on slide 78 and MUST stay true):
  - RabbitMQ on host port **5673**, management UI on **15673**;
  - Azure Service Bus emulator on **5672**, health endpoint on **5300**;
  - SQL Server on **1433**.
  - The environment is started with one command from `infra/` (`docker compose up -d`).
- M4 additionally needs an Azure subscription: Application Insights and a Service Bus namespace (Standard tier, Entra ID only), created by a one-time Bash script (`setup.sh`), removed by `teardown.sh`, and started using the commands printed by `run-demo.sh [asb|rabbit]`.
- Azure access uses Entra ID only: no SAS keys or connection strings with secrets.

---

## 3. Slide ↔ code cohesion (the contract)

**Bash migration:** script names and launch instructions below now reflect the
requested Bash conversion. Slides 41 and 78 must be updated to these names and
the two-terminal workflow when the deck is available; that deck update has not
been verified in this workspace.

### 3.1 Rules
1. **The slide is the reference for what is shown.** For each slide in 3.3, the named file MUST contain the slide's code. Code shown on a slide MUST NOT be invented, and MUST NOT come from a file other than the one the slide names.
2. **Allowed differences** between the slide and the file:
   - whitespace and indentation;
   - whole lines left out of the slide, including a method's or a class's body, closing braces, and `Console.WriteLine` lines;
   - a span replaced by `…` on the slide;
   - a trailing `// comment` that exists in the file but is left out on the slide;
   - a long line wrapped on the slide at an argument or operator boundary;
   - a slide line that is the leading or trailing part of a longer file line (for example the slide shows `r.AddTransportMessageForwarder(message =>` and the file has `.Routing(r => r.AddTransportMessageForwarder(message =>`).
3. **Not allowed:**
   - a line on the slide that does not exist in the file (comment lines included);
   - a line that is worded differently (for example an expression body on the slide and a block body in the file);
   - a literal on the slide where the file uses a constant, or the reverse;
   - a different variable, argument or type name.
4. **When the code must change** (a bug fix, a package upgrade, an API rename):
   - if the change touches a line that a slide shows, update that slide in the same change and re-check its layout (no text overflow);
   - otherwise the slide needs no change.
   - The cohesion table in 3.3 is updated whenever a status changes.
5. **Text references are part of the contract too.** File paths, folder paths, project names, queue names, keys, menu numbers and commands in slide labels, footers, speaker notes, `README.md`, `exercises/README.md` and `DEMO.md` MUST match the repository (see 3.5). Slide numbers in `README.md`, `DEMO.md` and the exercise READMEs MUST match the deck. `Program.cs` headers contain no slide numbers, so they never go stale.
6. **Slide-only code** (3.4) is labelled with an API or product name, never a repository path. It MUST NOT use identifiers that suggest it comes from the repository, such as `Transport.`, `Queues.` or `Names.`. The exception is a footer that clearly says "No native demo".
7. **Automated check (required).** The repository MUST include a small check, a script or a test, that:
   - extracts the monospace text of the slides listed in 3.3 from the `.pptx`;
   - verifies rule 1 against the named files, with the allowed differences of rule 2;
   - reports every line that does not match.
   Run it after every code or slide change.

### 3.2 Status values used below
- **OK:** matches today.
- **Fix code:** change the file so it contains the slide's lines.
- **Fix slide:** change the slide so it shows the file's lines.
- **Decide:** pick one side and record the decision.

### 3.3 Slides that show repository code

The verbatim slide code is in **Appendix A**. "Required file content" describes what must hold.

| Slide | Label on slide | File(s) | Required file content | Status today |
|---|---|---|---|---|
| 13 Slim vs fat | Contracts/Events.cs | `src/Contracts/Events.cs` | The `OrderPlaced` record (seven properties, in order) and the `OrderChanged` record exactly as shown, with the comment lines exactly as shown: `// FAT (event-carried state transfer): consumers get all they need.`, the SLIM comment, and the two "− consumers call back …" lines | **Fix code:** the file's FAT comment and the SLIM ± comments are worded differently |
| 17 Demo M1 | M1.Fundamentals/Program.cs | `src/M1.Fundamentals/Program.cs` | All shown lines, in order: the `FlakyPaymentService` initialiser with `// flip to true to compare`, the two section headings, the try/catch with both messages, the channel line with its comment, and the "202 Accepted" line | OK |
| 21 Names and metadata | Contracts/MessageMetadata.cs | `src/Contracts/MessageMetadata.cs` | The four `///` doc lines and the `MessageHeaders` class with seven constants and values (`ce-type`, `ce-source`, `schema-version`, `causation-id`, `traceparent`, `tenant-id`, `ce-time`), comments included | OK |
| 24 Upcast / dual publish | Contracts/Versioning.cs | `src/Contracts/Versioning.cs` | `OrderPlacedV1`, the BREAKING comment, `OrderPlacedV2` (with `SalesChannel = "web"`), `ToLatest` with its three switch arms, and `DualPublish.For` | OK |
| 28 Same handler, three ways (Wolverine panel) | Wolverine · M3.Wolverine/Payments.cs | `src/M3.Wolverine/Payments.cs` | `public static PaymentCharged Handle(ChargePayment cmd, Envelope envelope)`, the `Charging … correlation` line, the comment `// the returned event is published after the handler succeeds`, and the `return new PaymentCharged(… $"PAY-{…[..8]}" …)` line | OK |
| 28 (Rebus panel) | Rebus · M2.Rebus/Payments.cs | `src/M2.Rebus/Payments.cs` | `ChargePaymentHandler(IBus bus) : IHandleMessages<ChargePayment>`, the `Charging {cmd.OrderId}` line, and `bus.Publish(new PaymentCharged(cmd.OrderId, "PAY-1", cmd.Amount))` | OK |
| 29 One setting (broker.json) | broker.json · repository root | `broker.json` | Exactly the two shown lines (JSON with `//` comments) | **Fix code:** the file has different comments |
| 29 (Rebus panel) | Rebus · M2.Rebus/Transport.cs | `src/M2.Rebus/Transport.cs` | The `switch (broker)` exactly as shown: `t.UseAzureServiceBus(connection, inputQueue);` and `t.UseRabbitMq(rabbitUri, inputQueue);` with locals named `connection` and `rabbitUri`, followed by the two comment lines `// routing, Send/Publish/Subscribe, retries, sagas,` and `// handlers: identical on both brokers` | **Decide:** the file has an extra Entra ID branch that cannot be written in the slide's two-argument form. Recommended: drop the Entra ID branch from M2, M3, Ex2 and Ex3 (Azure mode only in M4), make the file match, and update slide 79 (row below). Alternative: show the Entra ID branch on the slide |
| 29 (Wolverine panel) | Wolverine · M3.Wolverine/Transport.cs | `src/M3.Wolverine/Transport.cs` | The `switch (broker)` exactly as shown: `opts.UseAzureServiceBusEmulator().AutoProvision();` and `opts.UseRabbitMq(rabbitUri).AutoProvision();`, plus the two comment lines about the `CommandQueue` and `Event` helpers. The helpers MUST NOT need a value captured from this switch | **Fix code:** the file has an Entra ID branch, `SystemQueuePrefix`, `UseQuorumQueues` and a captured RabbitMQ expression. Same decision as the Rebus panel. If quorum queues are wanted, show them on the slide |
| 34 Idempotent consumer | Reliability/Inbox.cs · SqlIdempotentHandler | `src/Reliability/Inbox.cs` | The two `///` lines, the transaction line, and the try/catch with error numbers 2627/2601 and its four comments | OK |
| 36 Retry tiers | Code: Reliability › ErrorClassifier.cs · Backoff.cs | `src/Reliability/Backoff.cs` | The formula line `delay = random(0, min(cap, base × 2^attempt))` and the line `// full jitter: no thundering herd` | **Fix code:** add the second line as a comment |
| 40 Same failure, Rebus and Wolverine (Rebus panel) | Rebus · retries, fail fast, error queue, re-drive (footer: "Rebus runs all of this in the M4 demo on the next slide") | `src/M4.ReliableOrders/M4.Payments/Program.cs`, `src/M4.ReliableOrders/M4.Orders/Redrive.cs` | The retry strategy, fail-fast and re-drive code M4 actually uses (Appendix B.1) | **Fix slide:** the slide shows `RebusHost.Build`, `Queues.Error` and `Queues.Parked`, 5 attempts and indexed headers, which exist nowhere. Replace it with M4's lines and name the files in the label |
| 40 (Wolverine panel) | Wolverine · error policies, dead letters in SQL, replay | none | Slide-only illustration (rule 6) | **Fix slide:** label it "Wolverine · illustration, not in the samples" and remove the line `Transport.CommandQueue<ChargePayment>(opts, broker, Names.Payments, sqlDeadLetters: true)`; that helper and parameter do not exist |
| 41 Retries and outbox (Rebus panel) | Rebus · M4.ReliableOrders › M4.Payments | `M4.Payments/Program.cs` and `M4.Payments/ChargePaymentFailedHandler.cs` | The `RetryStrategy` line, then the `Handle(IFailed<ChargePayment> failed)` excerpt with the `permanent` check, `Publish(new PaymentFailed(failed.Message.OrderId, reason))`, `Deadletter(failed.ErrorDescription)` and `Defer(Backoff.Next(deferrals, TimeSpan.FromSeconds(5)))` | **Fix slide:** the slide has the literal `"m4-error"` where the file uses `Transport.ErrorQueue`. Show `Transport.ErrorQueue`, and name both files in the label |
| 41 (Wolverine panel) | Wolverine · the same, as policies | none | Slide-only illustration | **Fix slide:** add "illustration, not in the samples" to the label |
| 49 Two processes, one timeline | M4.Shared/Telemetry.cs · the setup in both processes | `M4.Shared/Telemetry.cs`, `M4.Payments/Program.cs`, `M4.Payments/ChargePaymentHandler.cs` | The code shown in Appendix B.2 | **Fix both** (Appendix B.2): the file calls `UseAzureMonitor(o => …)` and the slide `UseAzureMonitor()`; the slide hard-codes `"payments-worker"` while the file uses a role-name parameter; the log lines differ; the `EnableDiagnosticSources` line is worded differently; the label names one file but the code comes from three |
| 58 Orchestration in code | M5.Sagas/Orchestration.cs (footer: slightly shortened; the file adds the happy path and the timeout handler) | `src/M5.Sagas/Orchestration.cs` | All shown lines, with their trailing comments (`// the saga's key is OrderId`, `// … one line per message type`, `// duplicate OrderPlaced: ignore`, `// 1  command`, `// 2  reply`, `// 3  command`, `// 4  reply: declined`, `// 5  undo step 1`, `// saga state is deleted`, `// the saga owns the deadline`) | **Fix code:** put the four `Data.… =` assignments on one line; make `Handle(StockReserved)` expression-bodied as on the slide (the timeout handler then always sends `ReleaseStock`, which is safe because compensation is idempotent); align the trailing comments. **Fix slide:** end the interface list with `, …`, because the file also implements `IHandleMessages<OrderTimeout>` |
| 59 Choreography in code | Inventory · publishes 2 and 4 / Payment and Orders · publish 3 and OrderCancelled | `src/M5.Sagas/Choreography.cs` | The comment lines `// Inventory service: reacts to 1 and 3`, `// the undo, triggered by an event`, `// Payment service: reacts to 2` and `// Orders service: reacts to 4`, and the three classes as shown. `OrdersHandlers` handles only `StockReleased`, expression-bodied | **Fix code:** add the four comment lines; change `OrdersHandlers` to the slide's form. Move the happy path (`PaymentCharged` → `OrderCompleted`) and the console output for completed and cancelled orders into a separate handler class, so the demo output does not change |
| 64 Pin names: Rebus | topology/frameworks/RebusTopology.cs · PinnedTopicNames throws for an unmapped type | `topology/frameworks/RebusTopology.cs` | Every shown line, in order. A `PinnedTopicNames` class that throws for an unmapped type. A RabbitMQ variant using `.Declarations(isDev, isDev, isDev)` (named in the footer) | **Fix code:** the file is worded differently (dictionary name, variable names, missing lines). Make it contain the slide excerpt |
| 65 Pin names: Wolverine | topology/frameworks/WolverineTopology.cs | `topology/frameworks/WolverineTopology.cs` | Every shown line, in order, with comments | **Fix code:** the file is worded differently |
| 66 Subscriptions as code | topology/servicebus/modules (footer) | `topology/servicebus/examples/billing.subscriptions.bicep`, `topology/servicebus/README.md` | The Bicep exactly as shown, including the first comment line, plus the `az stack group create … --action-on-unmanage deleteResources --deny-settings-mode none` command in the README | **Fix code** |
| 67 A shared bus in Git | Samples: topology/cicd/messaging-topology | `topology/cicd/messaging-topology/**` | The folder tree exactly as shown, and a `.github/CODEOWNERS` file with the three shown lines | Verify with the automated check |
| 68 RabbitMQ topology as code | Pipeline step … (generated from billing.rabbitmq.json) | `topology/rabbitmq/pipeline/apply-topology.sh`, `topology/rabbitmq/pipeline/examples/billing.rabbitmq.json` | The script MUST contain (or, when run, generate) the shown `rabbitmqadmin` lines for the billing example | **Fix code:** the shown lines are not in the script |
| 78 Lab run sheet | Terminal | the repository | Every command works as written from the repository root. Each line of the module list (module, framework, slides) is true | OK. Re-verify when slides move |
| 79 Running the demos in Azure | WHAT infra/azure/main.bicep DEPLOYS | `infra/azure/main.bicep`, `infra/azure/deploy.sh`, `infra/azure/README.md` | The entity list on the slide equals what the Bicep deploys. The claim "export them to run the samples against Azure" is true for every sample it covers | **Decide:** the slide lists `payments`, `orders-rebus` and topic `order-events` with four subscriptions, but the demos use other names and the frameworks create their own topology in dev. Recommended: the slide says the namespace is created and the frameworks create their queues and topics in dev, and states which samples honour `ASB_NAMESPACE` (after the slide 29 decision, M4 and the Reliability library only) |

### 3.4 Slide-only code (not in the repository; rule 6 applies)

| Slide | Label | Content |
|---|---|---|
| 16 | Azure Service Bus · Azure.Messaging.ServiceBus / RabbitMQ · RabbitMQ.Client 7 | Request–reply with session reply queues, and with Direct Reply-To |
| 28 | Native SDK · Azure.Messaging.ServiceBus | `ServiceBusProcessor` handler for `ChargePayment` |
| 38 | Service Bus · Azure.Messaging.ServiceBus / RabbitMQ · RabbitMQ.Client | Explicit settle: complete, dead-letter, abandon / ack, reject, nack. Footer: "No native demo: M4 (slide 41) …" |
| 39 | Azure.Messaging.ServiceBus · dead-letter re-drive | DLQ receiver and re-drive loop |
| 40, 41 | Wolverine panels | Error policies, durable inbox and outbox (after the fixes in 3.3) |
| 45 | Azure.Messaging.ServiceBus · sessions | Session processor with session state (ordering: slides only) |
| 46 | RabbitMQ.Client · single active consumer + consistent hash | Partitioned quorum queues (ordering: slides only) |
| 81 | Wolverine / Rebus | Two three-line handler excerpts for comparison. These match M3 and M2 in spirit; they are labelled with the framework, not a file |
| 82 | OpenTelemetry · one pipeline for every library | Generic OpenTelemetry set-up for all four libraries |

If a slide-only snippet is ever turned into runnable code, it moves to 3.3 and gets a file label.

### 3.5 Slide text that names repository items (must match)

| Slide | Text | Must match |
|---|---|---|
| 8 | Services Orders, Inventory, Payments, Shipping; messages PlaceOrder, OrderPlaced, ReserveStock, StockReserved, ChargePayment, PaymentCharged, ShipOrder, OrderShipped | Records with these names exist in `src/Contracts` |
| 17 | "Demo: M1.Fundamentals › Program.cs" | path |
| 13, 21, 24, 34, 36 | "Code: Contracts › Events.cs" · "Contracts › MessageMetadata.cs" · "Contracts › Versioning.cs" · "Reliability › Inbox.cs" · "Reliability › ErrorClassifier.cs · Backoff.cs" | paths |
| 29 | "Demo: M2.Rebus or M3.Wolverine · menu 2 + menu 1 in two terminals · change broker.json · run both again"; subtitle "All broker code lives in one file: Transport.cs" | menu numbers 1 = Orders, 2 = Payments; file names |
| 30 | Cards Orders / Shipping / OrderShipped; tasks 1–4 (route ShipOrder to the queue Shipping owns; send it, trying once without a route; handle it in Shipping and publish OrderShipped; subscribe to OrderShipped in Orders); footer "Code: exercises › Ex2.FirstEndpoint › Rebus.Starter or Wolverine.Starter (solutions next to them) · two terminals: menu 2, then menu 1" | Ex2 TODO numbering and folders (Section 12) |
| 37, 38 | "M4 (slide 41)" | M4 slide number |
| 41 | "Demo M4 (Rebus): ./run-demo.sh asb · keys ENTER x d k t f r in the orders-api window · runbook: src/M4.ReliableOrders/DEMO.md" | script name, keys, window name, path; update the deck from the former PowerShell script name |
| 42 | Gateway timeout "About 40% of calls" · Card declined "Amount over 1000" · "Currency XXX" · tasks 1–4 · "Press r and the same command arrives twice …" · footer "Code: exercises › Ex3.ReliableConsumer › Rebus.Starter or Wolverine.Starter (solutions next to them) · broker: broker.json" | the fake gateway's behaviour, Ex3 TODOs, keys, folders |
| 49 | Diagram "orders-api · POST /orders" → "Service Bus · queue m4-payments" → "payments-worker · charges the card"; list 1–6 (search one order across both processes; flaky: retries then a delay; declined: fail fast, PaymentFailed; duplicate charged once; Application Map; logs, one KQL query each); "Runbook: src/M4.ReliableOrders/DEMO.md · queries.kql" | role names, route, queue name, the six queries |
| 56 | "Demo: M5.Sagas › Choreography.cs" | path |
| 58, 59 | "dotnet run --project src/M5.Sagas -- orchestration" / "-- choreography"; step numbers in comments match the diagrams on slides 55 and 56 | command arguments, numbering |
| 69 | Ex4 questions | `exercises/Ex4.Topology/README.md` |
| 78 | Terminal commands; ports; module list M1, M2, M3, Ex2, M4, Ex3, M5 with slides 17, 28–29, 30, 41 + 49, 42, 56–59 | Section 2.3 and the slide map |
| Speaker notes | Slides 7, 29, 30, 32, 37, 38, 41, 49, 68 mention demo names, keys, slide numbers and exercise numbers | the same values |

---

## 4. Shared building blocks (requirements)

### 4.1 Contracts (`src/Contracts`)
- **Commands** (imperative names, exactly one receiver, the receiver owns the contract): `PlaceOrder`, `ChargePayment(OrderId, CustomerId, Amount, Currency)`, `RefundPayment`, `ReserveStock(OrderId, Lines)`, `ReleaseStock(OrderId, Reason)`, `ShipOrder(OrderId, ShipTo)`. Value types `OrderLine(Sku, Quantity, UnitPrice)` and `Address(Street, City, PostalCode, CountryCode)`.
- **Events** (past tense, 0..N subscribers, the publisher owns the contract):
  - `OrderPlaced` (fat; seven properties as on slide 13) and `OrderChanged(OrderId, Version, ChangeType)` (slim);
  - `PaymentCharged(OrderId, PaymentReference, Amount)`, `PaymentFailed(OrderId, Reason, IsPermanent)`, `PaymentRefunded`;
  - `StockReserved`, `StockReservationFailed`, `StockReleased`;
  - `OrderShipped(OrderId, TrackingNumber)`, `OrderCancelled(OrderId, Reason)`, `OrderCompleted(OrderId)`.
- **Metadata:** the `MessageHeaders` constants of slide 21, and a `MessageContext` with MessageId, CorrelationId, CausationId, MessageType, Source and OccurredAt. A message caused by another one keeps the CorrelationId and takes the parent's MessageId as its CausationId.
- **Versioning (slide 24):**
  - `OrderPlacedV1`, `OrderPlacedV2` with `Money`, and the type names `contoso.orders.order-placed.v1` / `.v2`;
  - an upcaster that turns v1 into v2 (currency `EUR`, sales channel `unknown`), reads tolerantly, and throws `UnknownMessageVersionException` (a permanent failure) for any other type;
  - a dual publisher that yields v2 and v1.

### 4.2 Hand-written reliability patterns (`src/Reliability`)
Reference code behind slides 34–36. It shows what the frameworks do under the hood. M4 and Ex3 reuse parts of it.
- **Error classification:** permanent = `BusinessRuleViolationException`, `UnknownMessageVersionException`, JSON or format errors, argument errors, and HTTP 400/403/404/422. Everything else is transient, with a bounded number of attempts.
- **Back-off:** full jitter, `random(0, min(cap, base × 2^attempt))`, base 1 s, cap 5 min. A retry-budget record documents the three tiers.
- **Inbox:**
  - an in-memory version;
  - a SQL version that inserts the MessageId and applies the business change in **one** transaction, and treats a primary-key violation (SQL error 2627/2601) as a duplicate: roll back and acknowledge.
- **Outbox:**
  - the outgoing message is written in the caller's transaction;
  - a relay publishes unsent rows in order and marks them sent;
  - only one relay is active at a time;
  - the relay publishes at least once, so consumers must be idempotent.

### 4.3 The broker setting
- Order of precedence:
  1. the first command-line argument (`asb` / `rabbit`, also `servicebus`, `azureservicebus`, `rabbitmq`);
  2. `--Broker` on the command line;
  3. the `BROKER` environment variable;
  4. `broker.json` next to the executable;
  5. the default, `rabbit`.
- An unknown value fails with a clear message.
- Every framework demo and exercise prints which broker it runs on at start-up: "RabbitMQ", "Azure Service Bus (emulator)" or "Azure Service Bus (<namespace>)".

---

## 5. M1 · Fundamentals: the cost of temporal coupling

**Intent.** Without any broker, show why asynchronous messaging decouples availability. A synchronous call to a payment service that is down **rejects** the order. The same request put on a queue is **accepted at once** and completed later, when the service is back. This opens the discussion of eventual consistency, "pending" states and idempotency on retry.

**Slide:** 17 (green dot). The slide shows an excerpt of `Program.cs` (cohesion: 3.3).

**Behaviour requirements**
1. A payment service stand-in can be up or down; the demo starts with it **down**. A single, clearly commented line lets the trainer flip it.
2. **Part 1:** a synchronous call. When the service is down, print `Order REJECTED because payments is down: … (n ms)`; when it is up, print `Order accepted, receipt … in n ms`.
3. **Part 2:**
   - put the same command on an in-process queue (a stand-in for a broker queue) and print `Order accepted (202 Accepted) in n ms – payment is pending`;
   - a background consumer retries every 500 ms and prints each failed attempt;
   - after about 1.2 s the service comes back (`...payments service comes back up`), and the consumer prints `charged … on attempt N`.
4. End with the discussion prompt: what did we trade? (eventual consistency, UX for "pending", idempotency on retry, where failures go, how the user learns the outcome)
5. No Docker containers needed.

**Usage (trainer)**
1. Run M1.
2. Section 1: rejected in about 100 ms.
3. Section 2: accepted at once; two failed attempts; charged after recovery.
4. Flip the flag to "up" and run again: ask "then why bother?" (availability, load levelling).
5. Ask the discussion questions.

**Acceptance:** with the service down, the output contains one REJECTED line, one Accepted line, at least one failed attempt and one "charged … on attempt N" line.

---

## 6. M2 · Rebus and M3 · Wolverine: two processes talking through the broker

**Intent.** The first framework demo:
- Two **separate processes**, in two terminals.
- **Orders** *sends* the `ChargePayment` command to the one queue that owns it.
- **Payments** handles it and *publishes* `PaymentCharged`; Orders *subscribes* and prints it.
- No broker types and no serialiser in user code.

Then the broker is switched with one setting and the same code runs again. M2 and M3 are the same scenario in two frameworks, so the audience compares frameworks, not scenarios.

**Slides:** 28 (green dot: the Payments handlers side by side with the native SDK) and 29 (broker.json and the two `Transport.cs` files). Cohesion: 3.3.

**Behaviour requirements (both frameworks)**
1. One console app per framework, with a menu: **1 = Orders** ("send ChargePayment, subscribe to PaymentCharged") and **2 = Payments** ("handle ChargePayment, publish PaymentCharged"). The menu numbers are fixed (slides 29 and 78).
2. **Orders:**
   - ENTER sends one `ChargePayment` (amount 99, currency EUR) and prints `→ ChargePayment <first 8 characters of the id>`;
   - every received event prints `← PaymentCharged <id8> (<reference>)`;
   - `q` quits.
3. **Payments:**
   - prints `Payments: listening on '<queue>' – press ENTER to stop`;
   - for each command, prints the "Charging …" line shown on slide 28 and emits `PaymentCharged`.
4. Each menu item loads only its own handlers.
5. **Queue and topic names:**
   - Rebus: `orders-rebus` (the Orders process) and `payments-rebus` (owned by Payments).
   - Wolverine: `payments-wolverine` (owned by Payments), topic or exchange `payment-events-wolverine`, and the Orders subscription or bound queue `orders-wolverine`.
6. **Framework-specific points** (must be visible in the code):
   - Rebus: explicit `Send`, `Publish`, `Subscribe` and type-based routing; returning from the handler acknowledges the message.
   - Wolverine: convention-found handler; the **returned** event is published after success; metadata on the `Envelope`.
7. Starting Orders before Payments works: the command waits in the queue.
8. **Must not contain:** retries configuration, error queues, outbox or inbox, sagas, deferral, ordering, telemetry, random failures.

**Usage (trainer)**
1. With `broker.json` = `rabbit`, open terminal A: run M2 (or M3) → **2**. Terminal B: run it again → **1**. Press ENTER a few times.
2. Show the queues, the exchange and a waiting message's headers in the RabbitMQ UI. Stop Payments first so a message stays in the queue.
3. Slide 29: stop both, change `broker.json` to `asb` (or pass `-- asb`), restart both, press ENTER. Same output.
4. Show the one broker-specific file. Close on "what a setting can't hide": where dead letters land, queue arguments, ordering.

**Acceptance:** on both brokers, each ENTER gives exactly one "Charging" line in Payments and one "← PaymentCharged" line in Orders.

---

## 7. M4 · Reliable orders (Rebus): run at slide 41, shown in Application Insights at slide 49

**Intent.** One realistic two-process flow that exercises **everything from the Reliability section in one run**. The trainer drives it live with single keys. Its telemetry lands in Application Insights, so the observability walk-through reuses the same run: there is no second demo.

**Slides:**
- 41 (green dot; Rebus retry configuration and the tier-2 handler);
- 49 (Application Insights walk-through; telemetry set-up and log lines);
- referenced from slides 32, 37, 38 and 40.

Cohesion: 3.3, Appendix B.

**Processes**

| Process | Role name in telemetry | Endpoint | Input queue | Responsibility |
|---|---|---|---|---|
| orders-api | `orders-api` | http://localhost:5180 (POST and GET /orders) | `m4-orders` | Takes orders (status Pending), sends `ChargePayment` through the outbox, subscribes to `PaymentCharged`/`PaymentFailed` and sets Paid/Failed, demo keys, re-drive |
| payments-worker | `payments-worker` | http://localhost:5181 | `m4-payments` | Charges the card through a fake gateway, with inbox, retry tiers and fail fast, and publishes the outcome through the outbox |

Shared queues: `m4-error` (error queue) and `m4-error-parked` (failures for a human). Message contracts for M4 are shared by both processes from **one** assembly (Rebus derives topic names and the message-type header from the type), and the customer **name** selects the scenario: `alice`, `flaky`, `declined` and `slow`.

**Behaviour requirements**
1. **Order placement:**
   - each order is a real HTTP POST, so it is the root of a trace;
   - the order row (status **Pending**) and the `ChargePayment` command are committed in **one SQL transaction**, the command going to the Rebus SQL outbox;
   - the order id is the CorrelationId; the MessageId is set explicitly;
   - the response is 202 Accepted.
2. **Status follows events:** `PaymentCharged` → **Paid**, `PaymentFailed` → **Failed**, using naturally idempotent updates.
3. **Charging, in one transaction:**
   - the inbox row for the MessageId (a duplicate is logged as "Duplicate message … skipped" and acknowledged);
   - the payment row;
   - `PaymentCharged`, written to the outbox.

   Throwing rolls all three back.
4. **Retry tiers:**
   - tier 1: 3 immediate attempts;
   - tier 2: a second-level handler that decides, either a delayed retry with back-off (base 5 s, at most 5 delays) or the error queue;
   - tier 3: `m4-error`.
5. **Fail fast:** a `BusinessRuleViolationException` (card declined) is not retried. It results in a `PaymentFailed` event **and** the message in `m4-error`.
6. **Fake gateway:**

   | Customer | Behaviour |
   |---|---|
   | `alice` (and other names) | succeeds |
   | `flaky` | times out until it has been delayed once |
   | `declined` | permanent failure |
   | `slow` | takes 15 s and logs "close this window NOW" |

7. **At-least-once:** if payments-worker dies mid-message, the message returns (RabbitMQ at once; Service Bus after a **30 s** lock) and is charged once.
8. **Re-drive (key r):** reads `m4-error` for about 10 s. Transient failures (timeouts) go back to their source queue; everything else goes to `m4-error-parked`. Each decision is printed.
9. **Keys** in the orders-api window (fixed: slide 41 and DEMO.md):

   | Key | Action |
   |---|---|
   | ENTER | alice order (happy path) |
   | `t` | flaky order (transient failure) |
   | `f` | declined order (permanent failure) |
   | `d` | duplicate: the same command twice with the same MessageId |
   | `k` | slow order (kill payments-worker while it works) |
   | `x` | crash orders-api after the commit, before the outbox sends |
   | `r` | re-drive the error queue |
   | `s` | status of the last 10 orders |
   | `w` | warm-up: 60 mixed orders over 2 minutes |
   | `q` | quit |

10. **Telemetry:**
    - Application Insights receives logs, traces and metrics when a connection string is present (from a settings file written by the setup script, or the environment); otherwise console only, with a one-line hint;
    - both processes carry their role names;
    - Rebus spans and trace context travel in message headers;
    - log scopes add OrderId, MessageId, CorrelationId, Attempt and DelayedRetries as columns;
    - one deliberately **bad** (interpolated) and one **good** (template) log line for the same event (slide 49).
11. **Queries:** six saved KQL queries (`queries.kql`):
    1. everything for one order across both processes;
    2. orders that needed retries;
    3. duplicates the inbox stopped;
    4. failures by process and exception type;
    5. the good vs bad log line;
    6. time from Pending to Paid (p50/p95/max).
12. **Scripts:** `setup.sh` (one-time Azure resources and settings file), `run-demo.sh [asb|rabbit]` (prints two Bash terminal commands; `--role payments` or `--role orders` builds and runs that process interactively), `teardown.sh` (deletes the resources). Scripts run in Bash on macOS/Linux, Git Bash on Windows, or WSL with prerequisites installed there. `DEMO.md` is the runbook (below).

**Usage at slide 41 (about 12 minutes, two windows side by side)**

Start both processes, then press **w** so warm-up traffic fills Application Insights while you talk.

| # | Key | What the audience sees | Trainer's line |
|---|---|---|---|
| 1 | ENTER | Pending → charged → Paid (press **s**) | "A command to one owner, an event to anyone. Pending first: eventual consistency." |
| 2 | x | orders-api dies after the commit; restart it; the order becomes Paid | "The dual-write problem. The command was in the database, in the same transaction." |
| 3 | d | "Duplicate message … skipped" | "At-least-once means duplicates. The inbox makes the second one a no-op." |
| 4 | k | Close payments-worker while it works; restart it; the message returns and is charged once | "Nothing is lost when a consumer dies, and nothing is half-done." |
| 5 | t | 3 quick failures → delayed retry about 5 s later → charged | "Immediate retries for blips, delayed retries for outages." |
| 6 | f | No retries → PaymentFailed → Failed; message in m4-error | "A declined card is a business outcome, not an outage." |
| 7 | r | Transient back to m4-payments, declined to m4-error-parked | "Every error queue needs an owner and a runbook. This is the runbook, as code." |
| 8 | – | Optional: switch broker.json, restart both, press t | "Same code, other broker." |

**Usage at slide 49 (about 10 minutes, no new run)**

Tabs ready: Transaction search, Failures, Application Map, Logs.
1. Follow the alice order end to end: request → send → handle → publish → handle. The gap between send and handle is the time in the queue.
2. The flaky order: three attempts, then the delayed retry.
3. Failures: the declined card, one attempt only, then PaymentFailed. "Who owns m4-error?"
4. Logs: query 3 (duplicates) and query 5 (the bad log line has empty columns).
5. Application Map: two nodes, drawn from the trace context in the message headers.
6. Close with the habits: name every process, carry the trace in the message, log templates not strings, tag business IDs.

**Day before:** run the setup script; start the local environment; run every key once; check the Application Map, Transaction search and Failures; save the queries; take fallback screenshots in case the venue blocks AMQP port 5671.

**Acceptance**
- Every key behaves as in the table on both brokers (with the 30 s lock delay on Service Bus).
- After a run, every order is Paid or Failed, except an `x` order before its restart.
- Exactly one payment row exists per charged order.
- The Application Map shows exactly `orders-api` and `payments-worker`, connected.

---

## 8. M5 · Sagas: orchestration vs choreography, with compensation

**Intent.** Run the same order flow twice:
- once with an **orchestrating saga**: one component knows the flow, sends commands, reacts to replies and owns the deadline;
- once by **choreography**: no coordinator; each service reacts to events.

The second order in each run has its card **declined**, so the audience sees the compensation (release stock) and the cancellation. The demo runs in memory, without a broker or database: only the transport line would change in production.

**Slides:** 55 and 56 (diagrams), 58 and 59 (green dots: code). Cohesion: 3.3.

**Behaviour requirements**
1. One console app; the argument `orchestration` (the default) or `choreography` selects the mode.
2. Three in-memory services: `orders`, `inventory` and `payment`. Commands are routed to one owning queue (`ReserveStock`/`ReleaseStock` → inventory, `ChargePayment` → payment).
3. Two orders: the first succeeds; the second is declined.
4. **Orchestration**, numbered to match the diagram on slide 55:
   1. send ReserveStock;
   2. StockReserved (a reply);
   3. send ChargePayment;
   4. PaymentFailed (a reply);
   5. send ReleaseStock (compensation), then publish OrderCancelled and complete the saga.

   The happy path publishes OrderCompleted. The saga correlates every message on OrderId, ignores a duplicate OrderPlaced, and schedules a 15-minute timeout that compensates and cancels.
5. **Choreography**, numbered to match the diagram on slide 56:
   1. OrderPlaced → Inventory reserves and publishes StockReserved;
   2. StockReserved → Payment publishes PaymentCharged or PaymentFailed;
   3. PaymentFailed → Inventory releases (compensation triggered by an event) and publishes StockReleased;
   4. StockReleased → Orders publishes OrderCancelled.

   Each service declares its own subscriptions.
6. The console shows each step with its number, the compensation as `COMPENSATE: release stock …`, and the final COMPLETED or CANCELLED per order.

**Usage**
1. Slides 55–56: explain the diagrams.
2. Slide 58: run orchestration. Order 1 completes; order 2 shows steps 1–4, then compensation and cancellation. Show the saga class: the numbers match the diagram.
3. Slide 59: run choreography. Same outcome, no coordinator. Ask "where would you look to see the whole flow?"

**Acceptance:** both modes print the happy and the compensated path in numbered order, in under 5 s, without Docker.

---

## 9. Ex1 · Command, event or query? (slide 18, no code)

**Intent.** Practise the distinction between commands, events and queries, and who owns each contract. Pairs, 5 minutes, then 5 minutes of discussion.

**Cards:**

| # | Card | Answer (speaker notes) |
|---|---|---|
| 1 | SendInvoice | Command; Invoicing owns it |
| 2 | InvoiceSent | Event; Invoicing owns it |
| 3 | CustomerAddressChanged | Event; Customer owns it |
| 4 | GetCustomer | Query. Keep it on HTTP, not a broker (the bonus answer) |
| 5 | ReserveStock | Command; Inventory owns it |
| 6 | StockLevelLow | Event; Inventory owns it |
| 7 | PriceListPublished | Event, a natural fat event; Pricing owns it |
| 8 | ApproveLoan | Command; Credit owns it (it can be refused) |

**Bonus:** which would you not put on a broker at all?

---

## 10. Exercises: common requirements
- Each code exercise has, per framework, a **Starter** (compiles and runs, but the scenario is incomplete until the TODOs are done) and a **Solution** (the same files, with every solved TODO marked by its number).
- TODO numbers are identical for Rebus and Wolverine.
- The broker connection is given in one shared file; attendees never write broker-specific code.
- Attendees keep the framework they chose in Ex2 for Ex3.
- `exercises/README.md` lists Ex1–Ex4 with slide, type and time: Ex1 slide 18, paper, 10 min · Ex2 slide 30, code, 15 min · Ex3 slide 42, code, 20 min · Ex4 slide 69, whiteboard, 15 min.

---

## 11. Ex2 · Your first endpoint (Rebus or Wolverine) – slide 30

**Intent.** Right after M2/M3, attendees wire the same pattern themselves, with a different pair of services so they cannot copy the demo:
- **Orders** sends `ShipOrder` to the queue **Shipping** owns;
- Shipping handles it and publishes `OrderShipped`;
- Orders subscribes and prints it.

Only concepts shown up to slide 30. 15 minutes (10 hands-on, 5 debrief).

**Requirements**
1. **Menu:** 1 = Orders ("send ShipOrder, subscribe to OrderShipped"), 2 = Shipping ("handle ShipOrder, publish OrderShipped"). The slide footer says "menu 2, then menu 1".
2. **Names:**
   - Rebus queues: `ex2-orders-rebus` and `ex2-shipping-rebus`.
   - Wolverine: queue `ex2-shipping-wolverine`, topic or exchange `ex2-shipping-events-wolverine`, subscription `ex2-orders-wolverine`.
3. **Given code:** the broker connection and hosting, plus (for Wolverine) helpers to declare a command queue (with an explicit listen yes/no, so attendees must decide) and an event (with an optional subscription). Also a sample `ShipOrder` factory, a short-id helper and the `OrderShipped` printing handler in Orders.
4. **The four TODOs**, matching slide 30:

   | TODO | File | Rebus | Wolverine |
   |---|---|---|---|
   | 1 Route `ShipOrder` to the queue Shipping owns | Orders | type-based routing map | declare the command queue, send only (no listen) |
   | 2 Send it on ENTER (try once before TODO 1 to see the "no route" error) | Orders | Send | SendAsync |
   | 3 Handle it in Shipping and publish `OrderShipped` (tracking number `TRK-<id8>`) | Shipping | publish from the handler | 3a listen on the queue; 3b declare the event; 3c return the event from the handler |
   | 4 Subscribe to `OrderShipped` | Orders | subscribe when the bus starts | declare the event with Orders' own subscription |

5. **Console:**
   - Orders prints `Orders: ENTER = ship an order · q = quit`, then `→ ShipOrder <id8>` per send and `← OrderShipped <id8> (TRK-…)` per event;
   - Shipping prints its listening line and `Shipping <id8> to <City>`.
6. **README:** intent and the flow diagram; folder table; how to run (two terminals); the tasks; "done when Orders prints `← OrderShipped … (TRK-…)` for every order"; stretch goals; debrief questions.
   - Stretch: switch the broker; run two Shipping windows (competing consumers); run two Orders windows (they share one subscription, so they compete too); find the queue, exchange and headers in the UI.
   - Debrief: where does the route live in each framework; what did the framework do that the native SDK slide did by hand; why does a command need a route and an event does not.

**Usage**
1. Show slide 30.
2. Attendees open their starter, with two terminals.
3. Walk around. Common snags: no route; Wolverine Orders listening on the shipping queue.
4. Debrief. If behind, do TODO 4 together on screen.

**Acceptance:** starters build and run; the solutions show every ENTER as a `Shipping …` line and an `← OrderShipped … (TRK-…)` line, on both brokers.

---

## 12. Ex3 · Make a flaky consumer reliable (Rebus or Wolverine) – slide 42

**Intent.** At the end of Reliability, attendees make a deliberately unreliable consumer reliable:
- retry transient failures with back-off;
- fail fast on poison messages;
- turn a declined card into a business event, not a retry storm;
- stretch: make the handler idempotent on the business key.

One console process sends and consumes through the broker. 20 minutes (15 + 5).

**Requirements**
1. **Fake gateway** (attendees must not change it):

   | Failure | When | Kind |
   |---|---|---|
   | Gateway timeout | about 40% of calls | transient |
   | Card declined | amount over 1000 | business outcome |
   | Unsupported currency | any currency other than EUR or DKK | poison |

   It counts charges per order and prints a red **DOUBLE CHARGE** line when an order is charged twice, green otherwise.
2. **Menu keys:**

   | Key | Sends |
   |---|---|
   | ENTER | normal payment (99.95 EUR) |
   | `d` | declined card (5000) |
   | `p` | poison (currency XXX) |
   | `r` | the same command twice (same order, new message id) |
   | `b` | burst of 10 |
   | `q` | quit |

3. **Queues:** `ex3-payments-rebus` / `ex3-payments-wolverine`; Rebus error queue `error`.
4. **The TODOs**, matching slide 42:
   1. route the command through the broker;
   2. handle it: call the gateway and publish `PaymentCharged`;
   3. retries: back-off for timeouts, fail fast on poison (Rebus: a retry strategy plus a second-level handler that dead-letters poison and defers timeouts 2, 4, 8 s, giving up after 3; Wolverine: one declarative policy per exception type, with in-process retries, then scheduled retries, then the error queue);
   4. a declined card becomes `PaymentFailed` (permanent), not a retry.

   Stretch: no double charge on **r**. Dedup on OrderId, because the two copies have different message ids.
5. **README:** "keep the framework from Exercise 2; tasks 1–2 are the moves you already made"; the failure table; tasks; stretch; debrief questions (where did the poison message end up and who is alerted; where does the retry policy live; what would this take with the native processor; what did not carry over when switching broker).

**Usage:** show slide 42 → attendees work in their Ex2 framework → debrief comparing Rebus (code) and Wolverine (declarative policies).

**Acceptance (solutions, both brokers):**
- ENTER → PaymentCharged, possibly after visible retries;
- **d** → PaymentFailed with no retries;
- **p** → error queue without delayed retries;
- **r** → no DOUBLE CHARGE.

---

## 13. Ex4 · Design your topology (slide 69, no code)

**Intent.** Pairs design the messaging topology of one real flow, or the fallback scenario, applying the Operations section.

**Requirements for `exercises/Ex4.Topology/README.md`**
- **Timing:** 15 minutes (10 in pairs, then two pairs present for 2 minutes each).
- **Fallback scenario:** Orders, Payments, Warehouse, Notifications and Analytics, owned by three teams. About a dozen hand-made Service Bus namespaces exist, subscriptions are added in the portal, and a recent deployment broke the Warehouse consumer.
- **Five questions:**
  1. One bus or several? Answer by criteria: blast radius, quotas, ownership, environments, network and cost.
  2. Which queues and topics or exchanges, and who owns each?
  3. For each subscription or binding: the filter, and who creates and deploys it (publisher, consumer or platform team; code, IaC or portal).
  4. Request–reply: a reply queue, sessions, or really a query on HTTP?
  5. A breaking change to `OrderPlaced`: expand, migrate, contract.
- **What good looks like:**
  - every queue, topic and subscription has exactly one owner;
  - consumers own their subscriptions, deployed with IaC by their own pipeline;
  - the number of namespaces follows a criterion you can name.

**Usage:** hand out the sheet or show slide 69; tie the answers back to slides 60–68.

**Topology snippets (`topology/`).** Teaching material behind slides 64–68. These files are not part of the build, but they are covered by the cohesion contract (3.3): each slide's code must be in the named file.

---

## 14. The day: timetable and order of use

| Time | Session | Demo | Exercise |
|---|---|---|---|
| 09:00–09:20 | Welcome & goals | – | – |
| 09:20–10:10 | Core principles | M1 (slide 17) | Ex1 (slide 18) |
| 10:10–10:30 | Contracts, ownership & versioning | – | – |
| 10:30–10:45 | Break | | |
| 10:45–11:25 | Messaging frameworks | M2/M3 (slides 28–29) | Ex2 (slide 30) |
| 11:25–12:15 | Reliability | M4 run (slide 41) | – |
| 12:15–13:00 | Lunch | | |
| 13:00–13:20 | Exercise | – | Ex3 (slide 42) |
| 13:20–13:55 | Ordering, correlation & observability | M4 in Application Insights (slide 49) | – |
| 13:55–14:30 | Sagas | M5 (slides 58–59) | – |
| 14:30–14:45 | Break | | |
| 14:45–15:45 | Operations & platform | – | Ex4 (slide 69) |
| 15:45–16:00 | Wrap-up | – | – |

The hidden timing slide (slide 7) MUST agree with this table. If a demo or exercise is added, moved or removed, update slide 7, the lab run sheet (slide 78), the green dots, `README.md` and `exercises/README.md` in the same change.

---

## 15. Acceptance checklist

1. The solution restores and builds with 0 errors (no floating package versions).
2. The automated cohesion check (3.1 rule 7) reports **no** mismatch for any slide in 3.3, and every text reference in 3.5 is true.
3. Each "Status today" in 3.3 that is not OK has been resolved, and the table has been updated.
4. M1, M2, M3, M4, M5, Ex2 and Ex3 meet their acceptance criteria; the framework ones on both brokers.
5. M2, M3 and Ex2 contain no retry, error-queue, outbox, saga, ordering or telemetry code.
6. No Aspire, and no ordering code, sessions, single-active-consumer or consistent-hash plugin anywhere.
7. Every `Program.cs` starts with the short description (Purpose / Show / Run), contains no slide numbers, and holds only the overall flow.
8. Green dots are on exactly slides 17, 28, 41, 58 and 59.
9. Slide numbers in `README.md`, `exercises/README.md`, the exercise READMEs and `DEMO.md` match the deck.

---

## Appendix A. Code shown on the slides today (verbatim, extracted from the deck)

This is the current slide side of the contract. Lines marked in 3.3 as "Fix slide" will change (see Appendix B). All other lines MUST be found in the named files.

### Slide 13 · Slim notification vs fat state transfer

*Contracts/Events.cs*

```csharp
// FAT (event-carried state transfer): consumers get all they need.
public sealed record OrderPlaced(
    Guid OrderId,
    Guid CustomerId,
    IReadOnlyList<OrderLine> Lines,
    decimal Total,
    string Currency,
    Address ShipTo,
    DateTimeOffset PlacedAt);

// SLIM (event notification): "something changed, ask me if you care".
//  − consumers call back: runtime coupling, publisher load,
//    stale reads: fetch "as of version N", never "latest"
public sealed record OrderChanged(Guid OrderId, long Version, string ChangeType);
```


### Slide 17 · Demo M1: the cost of temporal coupling

*M1.Fundamentals/Program.cs*

```csharp
var payments = new FlakyPaymentService { IsUp = false };   // flip to true to compare

Console.WriteLine("── 1. Synchronous request/response (HTTP-style) ──");
try
{
    // Caller blocks; availability(caller) = availability(caller) × availability(payments)
    var receipt = await payments.ChargeAsync(new ChargePayment(Guid.NewGuid(), Guid.NewGuid(), 99m, "EUR"));
    Console.WriteLine($"Order accepted, receipt {receipt} in {sw.ElapsedMilliseconds} ms");
}
catch (Exception ex)
{
    Console.WriteLine($"Order REJECTED because payments is down: {ex.Message} ({sw.ElapsedMilliseconds} ms)");
}

Console.WriteLine("── 2. Asynchronous messaging (queue in the middle) ──");
var queue = Channel.CreateUnbounded<ChargePayment>();      // stands in for a broker queue
await queue.Writer.WriteAsync(new ChargePayment(Guid.NewGuid(), Guid.NewGuid(), 99m, "EUR"));
Console.WriteLine($"Order accepted (202 Accepted) in {sw.ElapsedMilliseconds} ms – payment is pending");
```


### Slide 21 · Names and metadata that travel well

*Name pattern on the slide (must match `OrderPlacedTypes` and the `ce-type` comment)*

```text
contoso.orders.order-placed.v2
```

*Contracts/MessageMetadata.cs*

```csharp
/// The metadata every message in the estate should carry, independent of broker.
/// Map these to native properties where they exist (ASB: MessageId, CorrelationId, Subject,
/// ContentType; AMQP 0-9-1: message_id, correlation_id, type, content_type) and to
/// application properties / headers for the rest. Names follow CloudEvents where possible.
public static class MessageHeaders
{
    public const string MessageType    = "ce-type";          // e.g. "contoso.orders.order-placed.v2"
    public const string Source         = "ce-source";        // e.g. "/contoso/orders-api"
    public const string SchemaVersion  = "schema-version";   // "2"
    public const string CausationId    = "causation-id";     // MessageId of the message that caused this one
    public const string TraceParent    = "traceparent";      // W3C trace context (OpenTelemetry)
    public const string TenantId       = "tenant-id";
    public const string OccurredAt     = "ce-time";
}
```


### Slide 24 · Upcast at the edge, dual publish at the source

*Contracts/Versioning.cs*

```csharp
public sealed record OrderPlacedV1(Guid OrderId, Guid CustomerId, decimal Total);
// v2 here is BREAKING: Total became Money (amount + currency) => new type + new ce-type.
public sealed record OrderPlacedV2(Guid OrderId, Guid CustomerId, Money Total, string SalesChannel = "web");

/// <summary>Consumer-side upcaster: everything is converted to the latest model at the edge.</summary>
public static OrderPlacedV2 ToLatest(string ceType, ReadOnlySpan<byte> body) => ceType switch
{
    OrderPlacedTypes.V2 => JsonSerializer.Deserialize<OrderPlacedV2>(body, Tolerant)!,
    OrderPlacedTypes.V1 => Upcast(JsonSerializer.Deserialize<OrderPlacedV1>(body, Tolerant)!),
    _ => throw new UnknownMessageVersionException(ceType)   // permanent failure → dead-letter
};

/// Publisher-side: during the deprecation window publish both versions.
public static IEnumerable<(string ceType, object payload)> For(OrderPlacedV2 e)
{
    yield return (OrderPlacedTypes.V2, e);
    yield return (OrderPlacedTypes.V1, new OrderPlacedV1(e.OrderId, e.CustomerId, e.Total.Amount));
}
```


### Slide 28 · Same ChargePayment handler, three ways

*Native SDK · Azure.Messaging.ServiceBus (slide-only)*

```csharp
processor.ProcessMessageAsync += async args =>
    var cmd = Json.Deserialize<ChargePayment>(m.Body.ToMemory().Span);
    await PaymentGateway.ChargeAsync(cmd, args.CancellationToken);
    await args.CompleteMessageAsync(m);
```

*Wolverine · M3.Wolverine/Payments.cs*

```csharp
public static PaymentCharged Handle(ChargePayment cmd, Envelope envelope)
{
    Console.WriteLine($"Charging {cmd.OrderId}, correlation {envelope.CorrelationId}");
    // the returned event is published after the handler succeeds
    return new PaymentCharged(cmd.OrderId, $"PAY-{cmd.OrderId.ToString()[..8]}", cmd.Amount);
```

*Rebus · M2.Rebus/Payments.cs*

```csharp
public class ChargePaymentHandler(IBus bus) : IHandleMessages<ChargePayment>
    public async Task Handle(ChargePayment cmd)
        Console.WriteLine($"Charging {cmd.OrderId}");
        await bus.Publish(new PaymentCharged(cmd.OrderId, "PAY-1", cmd.Amount));
```


### Slide 29 · One setting changes the broker

*broker.json · repository root*

```jsonc
{ "Broker": "rabbit" }          // or "asb" · per run: dotnet run -- asb · or BROKER=asb
// read by M2.Rebus, M3.Wolverine and Exercises 2 + 3 (Rebus and Wolverine starters + solutions)
```

*Rebus · M2.Rebus/Transport.cs*

```csharp
switch (broker)
{
  case Broker.ServiceBus:
    t.UseAzureServiceBus(connection, inputQueue);
    break;
  case Broker.RabbitMq:
    t.UseRabbitMq(rabbitUri, inputQueue);
    break;
}
// routing, Send/Publish/Subscribe, retries, sagas,
// handlers: identical on both brokers
```

*Wolverine · M3.Wolverine/Transport.cs*

```csharp
switch (broker)
{
  case Broker.ServiceBus:
    opts.UseAzureServiceBusEmulator().AutoProvision();
    break;
  case Broker.RabbitMq:
    opts.UseRabbitMq(rabbitUri).AutoProvision();
    break;
}
// + helpers: CommandQueue and Event –
// the demos call them and never name a broker
```


### Slide 34 · Idempotent consumer with an inbox table

*Reliability/Inbox.cs · SqlIdempotentHandler*

```csharp
/// The production version: insert into the inbox and apply the business change in ONE
/// database transaction. A duplicate hits the primary key and is skipped.
await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);
try
{
    await using (var cmd = new SqlCommand(
        "INSERT INTO dbo.Inbox (MessageId, Consumer) VALUES (@id, @consumer)", conn, tx))
    …
    await businessChange(conn, tx);
    await tx.CommitAsync(ct);
    return true;                                        // processed now
}
catch (SqlException ex) when (ex.Number is 2627 or 2601) // PK violation → duplicate
{
    await tx.RollbackAsync(ct);
    return false;                                       // already processed → just ack
}
```


### Slide 36 · Will a retry help? Then retry in tiers

*Reliability/Backoff.cs (formula)*

```text
delay = random(0, min(cap, base × 2^attempt))
```

*Reliability/Backoff.cs (comment)*

```text
// full jitter: no thundering herd
```


### Slide 40 · Same failure, with Rebus and Wolverine

*Rebus panel (to be replaced, Appendix B.1)*

```csharp
RebusHost.Build(broker, Queues.Payments, c => c
  .Options(o =>
  {
    o.RetryStrategy(errorQueueName: Queues.Error,
                    maxDeliveryAttempts: 5);
    o.FailFastOn<BusinessRuleViolationException>();
  }));
// handler returns = ack · throws = retry, then "error"
// re-drive: a second bus reads "error"
r.AddTransportMessageForwarder(m =>
{
  var source = m.Headers[Headers.SourceQueue];
  var details = m.Headers[Headers.ErrorDetails];
  var transient = details.Contains("Timeout");
  return Task.FromResult(ForwardAction.ForwardTo(
    transient ? source : Queues.Parked));
});
```

*Wolverine panel (slide-only illustration)*

```csharp
opts.Policies
  .OnException<BusinessRuleViolationException>()
  .MoveToErrorQueue();
opts.Policies.OnException<TimeoutException>()
  .RetryWithCooldown(50.Milliseconds(),
                     250.Milliseconds(), 1.Seconds())
  .Then.MoveToErrorQueue();
Transport.CommandQueue<ChargePayment>(opts, broker,
  Names.Payments, sqlDeadLetters: true);  // -> SQL
opts.Policies.UseDurableInboxOnAllListeners(); // dedup
// handler: plain method, no settle calls
public static void Handle(ChargePayment cmd) { ... }
// re-drive: mark rows replayable, Wolverine resends
UPDATE wolverine.wolverine_dead_letters
   SET replayable = 1
 WHERE exception_type LIKE '%TimeoutException%';
```


### Slide 41 · Retries and outbox, the framework way

*Wolverine panel (slide-only illustration)*

```csharp
opts.Policies.UseDurableOutboxOnAllSendingEndpoints();   // outgoing messages committed with your DB tx
opts.Policies.UseDurableInboxOnAllListeners();           // incoming envelopes persisted → dedup on envelope id
opts.Policies.OnException<BusinessRuleViolationException>().MoveToErrorQueue();   // permanent → DLQ now
opts.Policies.OnException<TimeoutException>()
    .RetryWithCooldown(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(250))  // tier 1: in-process
    .Then.ScheduleRetry(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2))
    .Then.MoveToErrorQueue();                                                           // tier 3: DLQ
```

*Rebus · M4.Payments/Program.cs + ChargePaymentFailedHandler.cs*

```csharp
o.RetryStrategy(errorQueueName: "m4-error", maxDeliveryAttempts: 3, secondLevelRetriesEnabled: true);
public async Task Handle(IFailed<ChargePayment> failed)
{
    var permanent = failed.Exceptions.Any(e => e.Type.Contains(nameof(BusinessRuleViolationException)));
    if (permanent)
    {
        await bus.Publish(new PaymentFailed(failed.Message.OrderId, reason));
        await bus.Advanced.TransportMessage.Deadletter(failed.ErrorDescription);
        return;
    }
    await bus.Advanced.TransportMessage.Defer(Backoff.Next(deferrals, TimeSpan.FromSeconds(5)));
```


### Slide 49 · Two processes, one timeline

*M4 telemetry and log lines (to be updated, Appendix B.2)*

```csharp
builder.Services.AddOpenTelemetry()
    .UseAzureMonitor()          // logs, traces, Live Metrics
    .ConfigureResource(r => r.AddService("payments-worker"))
    .WithTracing(t => t.AddRebusInstrumentation());
// Rebus: spans + trace context in the message headers
.Options(o => o.EnableDiagnosticSources())
// GOOD: OrderId and Amount become queryable columns
log.LogInformation("Charged {Amount} for {OrderId}", a, id);
// BAD: one string, nothing to filter on
log.LogInformation($"Charged {a} for {id}");
```


### Slide 58 · Orchestration in code: a Rebus saga

*M5.Sagas/Orchestration.cs*

```csharp
public class OrderSaga(IBus bus) : Saga<OrderSagaData>, IAmInitiatedBy<OrderPlaced>,
    IHandleMessages<StockReserved>, IHandleMessages<PaymentFailed>, IHandleMessages<PaymentCharged>
{
    protected override void CorrelateMessages(ICorrelationConfig<OrderSagaData> c)
    {
        c.Correlate<OrderPlaced>(m => m.OrderId, d => d.OrderId);      // the saga's key is OrderId
        c.Correlate<StockReserved>(m => m.OrderId, d => d.OrderId);    // … one line per message type
    }
    public async Task Handle(OrderPlaced m)
    {
        if (!IsNew) return;                                             // duplicate OrderPlaced: ignore
        Data.OrderId = m.OrderId; Data.CustomerId = m.CustomerId; Data.Total = m.Total; …
        await bus.Send(new ReserveStock(m.OrderId, m.Lines));                         // 1  command
        await bus.DeferLocal(TimeSpan.FromMinutes(15), new OrderTimeout(m.OrderId));  // the saga owns the deadline
    }
    public Task Handle(StockReserved m) =>                                            // 2  reply
        bus.Send(new ChargePayment(m.OrderId, Data.CustomerId, Data.Total, Data.Currency)); // 3  command
    public async Task Handle(PaymentFailed m)                                         // 4  reply: declined
    {
        await bus.Send(new ReleaseStock(m.OrderId, m.Reason));                        // 5  undo step 1
        await bus.Publish(new OrderCancelled(m.OrderId, m.Reason));
        MarkAsComplete();                                                             // saga state is deleted
    }
}
```


### Slide 59 · Choreography in code: plain Rebus handlers

*M5.Sagas/Choreography.cs · Inventory*

```csharp
// Inventory service: reacts to 1 and 3
public class InventoryHandlers(IBus bus) :
  IHandleMessages<OrderPlaced>,
  IHandleMessages<PaymentFailed>
{
  public async Task Handle(OrderPlaced e)
  {
    Stock.Reserve(e.OrderId);
    await bus.Publish(new StockReserved(e.OrderId));
  }
  // the undo, triggered by an event
  public async Task Handle(PaymentFailed e)
  {
    Stock.Release(e.OrderId);
    await bus.Publish(new StockReleased(e.OrderId));
  }
}
```

*M5.Sagas/Choreography.cs · Payment and Orders*

```csharp
// Payment service: reacts to 2
public class PaymentHandlers(IBus bus)
  : IHandleMessages<StockReserved>
{
  public async Task Handle(StockReserved e)
  {
    object result = Cards.TryCharge(e.OrderId)
      ? new PaymentCharged(e.OrderId, "PAY-1", 99m)
      : new PaymentFailed(e.OrderId, "Declined", true);
    await bus.Publish(result);
  }
}
// Orders service: reacts to 4
public class OrdersHandlers(IBus bus)
  : IHandleMessages<StockReleased>
{
  public Task Handle(StockReleased e) =>
    bus.Publish(new OrderCancelled(
      e.OrderId, "Declined"));
}
```


### Slide 64 · Pin names and defaults: Rebus

*topology/frameworks/RebusTopology.cs*

```csharp
static readonly Dictionary<Type, string> Topics = new()
{
    [typeof(OrderPlaced)]    = "orders.order-placed.v1",           // a contract name, not a class name
    [typeof(OrderCancelled)] = "orders.order-cancelled.v1",
};
services.AddRebus(configure => configure
    .Transport(t =>
    {
        var asb = t.UseAzureServiceBus(connectionString, "billing", credential)
            .SetMessagePeekLockDuration(TimeSpan.FromMinutes(1))      // explicit defaults
            .SetDefaultMessageTimeToLive(TimeSpan.FromDays(14))
            .AutomaticallyRenewPeekLock();
        if (!env.IsDevelopment())                                     // test + prod: topology from IaC
            asb.DoNotCreateQueues().DoNotCheckQueueConfiguration().DoNotConfigureTopic();
    })
    .Serialization(s => s.UseCustomMessageTypeNames()              // message type header pinned
        .AddWithCustomName<OrderPlaced>("orders.order-placed.v1")
        .AddWithCustomName<OrderCancelled>("orders.order-cancelled.v1"))
    .Options(o =>
    {
        o.Decorate<ITopicNameConvention>(_ => new PinnedTopicNames(Topics));      // topic name pinned
        o.RetryStrategy(errorQueueName: "billing.error", maxDeliveryAttempts: 5); // below MaxDeliveryCount
    }),
    onCreated: bus => bus.Subscribe<OrderPlaced>());   // dev: creates the subscription; test + prod: no-op
```


### Slide 65 · Pin names and defaults: Wolverine

*topology/frameworks/WolverineTopology.cs*

```csharp
[MessageIdentity("orders.order-placed.v1")]                       // message type name pinned
public record OrderPlaced(Guid OrderId, decimal Total);
builder.UseWolverine(opts =>
{
    var asb = opts.UseAzureServiceBus(ns, credential);
    if (builder.Environment.IsDevelopment())
        asb.AutoProvision();                                          // test + prod: topology from IaC
    opts.PublishMessage<OrderPlaced>()
        .ToAzureServiceBusTopic("orders.order-placed.v1");            // topic name pinned
    opts.ListenToAzureServiceBusSubscription("billing", sub =>         // subscription name pinned
        {
            sub.MaxDeliveryCount = 10;                                // explicit defaults
            sub.LockDuration = TimeSpan.FromMinutes(1);
            sub.DefaultMessageTimeToLive = TimeSpan.FromDays(14);
            sub.DeadLetteringOnMessageExpiration = true;
        })
        .FromTopic("orders.order-placed.v1");
    opts.Policies.OnException<TimeoutException>()
        .RetryWithCooldown(50.Milliseconds(), 250.Milliseconds(), 1.Seconds());  // below MaxDeliveryCount
});
```


### Slide 66 · Subscriptions as code: one reusable Bicep module

*topology/servicebus/examples/billing.subscriptions.bicep*

```bicep
// billing/infra/subscriptions.bicep  ·  owned by Billing
param namespaceName string
param billingPrincipalId string
var subscriptions = [
  { topic: 'orders.order-placed.v1',     filter: '' }
  { topic: 'orders.order-cancelled.v1',  filter: '' }
  { topic: 'payments.payment-failed.v1', filter: 'IsPermanent = true' }
]
module sub 'br/platform:servicebus/subscription:1.0' = [
  for s in subscriptions: {
    name: 'sub-billing-${uniqueString(s.topic)}'
    params: {
      namespaceName: namespaceName
      topicName: s.topic
      subscriptionName: 'billing'
      sqlFilter: s.filter
      receiverPrincipalId: billingPrincipalId   // Data Receiver
    }
  }
]
```

*topology/servicebus/README.md*

```bash
az stack group create … --action-on-unmanage deleteResources --deny-settings-mode none
```


### Slide 67 · A shared bus in Git and CI/CD

*topology/cicd/messaging-topology (tree and CODEOWNERS)*

```bash
messaging-topology/
  platform/   namespace.bicep          # platform team
  modules/    topic · subscription · queue  # versioned
  teams/
    orders/   topics.bicep             # publisher
    billing/  subscriptions.bicep      # consumer
              subscriptions.test.bicepparam
              subscriptions.prod.bicepparam
  scripts/    check-names.sh · deploy.sh
  .github/    workflows/topology.yml
# .github/CODEOWNERS
*                  @contoso/messaging-platform
/teams/orders/     @contoso/orders
/teams/billing/    @contoso/billing
```


### Slide 68 · RabbitMQ topology as code: self-hosted or managed

*topology/rabbitmq/pipeline/apply-topology.sh*

```bash
# topology/rabbitmq/pipeline/apply-topology.sh prod billing.rabbitmq.json <last-deployed-ref>
rmq="rabbitmqadmin --node prod --vhost contoso"     # [prod] = config entry written from the vault
$rmq queues declare   --name billing.order-placed      --type quorum --durable true
$rmq queues declare   --name billing.order-placed.dlq  --type quorum --durable true
$rmq policies declare --name billing.order-placed --pattern '^billing\.order-placed$' \
     --apply-to queues --priority 10 --definition '{"delivery-limit":10,"message-ttl":1209600000,…}'
$rmq bindings declare --source orders.order-placed.v1 --destination-type queue \
     --destination billing.order-placed --routing-key '#'
# gone from the file since <last-deployed-ref>?  ->  $rmq queues delete --name … --idempotently
```


### Slide 78 · Lab run sheet

*Lab run sheet · Terminal*

```bash
cd infra && docker compose up -d
dotnet run --project src/M1.Fundamentals
cd src/M4.ReliableOrders && ./run-demo.sh asb  # M4: run setup.sh once; run the two printed commands
dotnet run --project src/M3.Wolverine -- asb   # menus 2 + 1 · no argument = broker.json
dotnet run --project src/M2.Rebus -- rabbit    # menus 2 + 1 · no argument = broker.json
```


### Slide 79 · Running the demos in Azure (optional)

*infra/azure/README.md (deploy command)*

```bash
cd infra/azure && ./deploy.sh <resource-group> westeurope    # optionally add --with-rabbitmq
# prints ASB_NAMESPACE + SQL_CONNECTION: export them to run the samples against Azure
# then once, as Entra admin: run sql-grant-identity.sql against the Messaging database
```

---

## Appendix B. Required replacement code for slides 40 and 49

In both cases the slide and the file change **together**, so that afterwards they are identical within the allowed differences of 3.1.

### B.1 Slide 40, Rebus panel
- **New label:** `Rebus · M4.Payments/Program.cs · M4.Orders/Redrive.cs`.
- **The slide shows these lines.** Wrapping at argument boundaries is allowed, because the panel is narrow:

```csharp
.Options(o =>
{
    o.RetryStrategy(errorQueueName: Transport.ErrorQueue, maxDeliveryAttempts: 3, secondLevelRetriesEnabled: true);
    o.FailFastOn<BusinessRuleViolationException>();
})
// handler returns = ack · throws = retry, then m4-error
// re-drive: a second bus reads m4-error
r.AddTransportMessageForwarder(message =>
{
    var source = message.Headers.GetValueOrDefault(Headers.SourceQueue) ?? Transport.PaymentsQueue;
    var details = message.Headers.GetValueOrDefault(Headers.ErrorDetails) ?? "";
    var transient = details.Contains(nameof(TimeoutException));
    return Task.FromResult(ForwardAction.ForwardTo(transient ? source : Transport.ParkedQueue));
});
```

- **The files must contain these lines**, including:
  - the comment `// handler returns = ack · throws = retry, then m4-error` in `M4.Payments/Program.cs`;
  - the comment `// re-drive: a second bus reads m4-error` in `M4.Orders/Redrive.cs`.
- **Wolverine panel:** label it `Wolverine · illustration, not in the samples` and delete the `Transport.CommandQueue<ChargePayment>(…, sqlDeadLetters: true)` line (two lines on the slide).

### B.2 Slide 49, telemetry and log lines
- **New label:** `M4 · Telemetry.cs (both processes) · M4.Payments/Program.cs · ChargePaymentHandler.cs`.
- **The slide shows these lines, and the files contain them:**

```csharp
builder.Services.AddOpenTelemetry()
    .UseAzureMonitor()          // logs, traces, Live Metrics
    .ConfigureResource(r => r.AddService(roleName))   // "orders-api" or "payments-worker"
    .WithTracing(t => t.AddRebusInstrumentation());
// Rebus: spans + trace context in the message headers
o.EnableDiagnosticSources();
// GOOD: OrderId and Amount become queryable columns
log.LogInformation("Charged {Amount} for {OrderId}", cmd.Amount, cmd.OrderId);
// BAD: one string, nothing to filter on
log.LogInformation($"Charged {cmd.Amount} for {cmd.OrderId}");
```

- **Code changes:**
  1. Telemetry calls `UseAzureMonitor()` with no arguments. The Azure Monitor distribution reads `APPLICATIONINSIGHTS_CONNECTION_STRING` from configuration (verify this for the pinned version), and the settings file and environment variable keep providing it.
  2. The resource uses the `roleName` parameter.
  3. Payments' Rebus options contain the comment line and `o.EnableDiagnosticSources();`, and so do Orders'.
  4. The payment handler logs exactly the GOOD and BAD lines above; drop the payment reference from the template.
  5. The KQL query 5 ("Charged" lines) keeps working.
- **Slide changes:** `r.AddService(roleName)` with its comment; the two log lines with `cmd.Amount` / `cmd.OrderId`; `o.EnableDiagnosticSources();` instead of `.Options(o => o.EnableDiagnosticSources())`.
