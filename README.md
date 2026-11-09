# Event & Message-Oriented Solutions — Training Code Samples (C# / .NET 10)

Companion code for the full-day training. Every module maps to a block in the agenda.
The same small domain (Orders → Payments → Inventory → Shipping) is used throughout so
that participants can compare the two frameworks (Wolverine, Rebus) on both **Azure Service Bus** and **RabbitMQ**.
Native SDK code is shown on the slides only.

Modules are numbered in the order they appear in the slides. **Each demo's `Program.cs` starts with a short description**
(purpose, what to show, how to run) and holds the overall flow; the details live in the files next to it.

| Folder | Slides | What it shows |
|---|---|---|
| `src/M1.Fundamentals` | 17 | Sync (HTTP) vs async (queue) call: the cost of temporal coupling. No broker needed |
| `src/M2.Rebus` | 28 · 29 | **Rebus: Orders and Payments as two processes.** Menu 1 Orders sends ChargePayment and subscribes to PaymentCharged; menu 2 Payments handles the command and publishes the event. The broker is a setting (`broker.json`) |
| `src/M3.Wolverine` | 28 · 29 | **Wolverine: the same two processes, same menu numbers.** The Payments handler returns the event (cascading message) |
| `src/M4.ReliableOrders` | 41 · 49 | **Rebus, everything from Reliability in one run, traced in Application Insights.** orders-api + payments-worker: outbox (crash after commit), inbox (duplicates), retry tiers (immediate + delayed), fail fast, error queue + re-drive, at-least-once (kill the consumer). Run at slide 41; show the same run in Application Insights at slide 49. Runbook in `DEMO.md` |
| `src/M5.Sagas` | 56 · 58 · 59 | Rebus, in memory: the same order flow as orchestration (`-- orchestration`) and as choreography (`-- choreography`), with compensation |
| `src/Contracts` | 20–24 | Commands vs events, slim vs fat, metadata envelope, versioning (v1/v2, upcasting, tolerant reader) |
| `src/Reliability` (library) | 33 · 35 | The hand-written patterns the frameworks replace: idempotent consumer (inbox table), transactional outbox, error classification, back-off + jitter. Reference code for the slides |
| `exercises/` | 18 · 30 · 42 · 69 | **Ex1** command, event or query (slide only) · **Ex2** your first endpoint: wire Orders → Shipping with Rebus or Wolverine, starter + solution · **Ex3** flaky consumer, Rebus or Wolverine starter + solution · **Ex4** topology design handout. Short description at the top of each `Program.cs` |
| `infra/azure/` | 79 | Bicep for an Azure demo environment: Service Bus (Entra ID only), Azure SQL (Entra-only), managed identity + RBAC, optional RabbitMQ in ACI |
| `infra/` | 78 | docker-compose (RabbitMQ + Service Bus emulator + SQL Server), emulator config, Bicep, RabbitMQ Messaging Topology Operator YAML |

## Running locally

For Exercises 2 and 3 without Docker, follow the
[Azure Service Bus exercise setup](exercises/azure/README.md).
It provisions a dedicated namespace with Bicep and uses your Azure CLI sign-in.

### Dev Container (optional)

Install Docker Desktop (Linux containers on Windows) and the VS Code
**Dev Containers** extension (`ms-vscode-remote.remote-containers`). Open this
repository and select **Dev Containers: Reopen in Container**.

The container includes the .NET 10 SDK, Azure CLI with Bicep, GitHub CLI, Git,
Bash, Python 3, curl, jq, OpenSSL, ShellCheck, and Docker with Compose v2.
C#, Bicep, container and ShellCheck editor extensions are installed automatically.
The first creation verifies the tools and restores the solution.
If the creation restore fails because `api.nuget.org` is unreachable, keep the
container open and run `dotnet restore MessagingTraining.slnx --source https://www.nuget.org/api/v2/`
in its terminal, then continue below. Do not disable TLS verification.

Inside the container terminal, from the repository root:

```bash
docker compose -f infra/docker-compose.yml up -d
dotnet build MessagingTraining.slnx --no-restore
dotnet run --project tests/M4.Telemetry.Tests
```

Docker-in-Docker keeps the training services isolated from the host's containers
and preserves the samples' `localhost` addresses and Compose bind-mount paths.
It requires a **privileged container**: use only with trusted repository content.
Services are not started automatically. Their images/data use the Docker feature's
volume; a container rebuild still requires starting Compose again. Stop them with
`docker compose -f infra/docker-compose.yml stop`.

The Orders (5180), Payments (5181), RabbitMQ UI (15673) and emulator health (5300)
ports are forwarded to the host. Run the existing debug configurations and scripts
inside the container. Host Docker services do not serve this isolated environment.

For M4 Azure resources, sign in **inside** the container with
`az login --use-device-code`, then follow its [runbook](src/M4.ReliableOrders/DEMO.md).
Host Azure credentials are not mounted; sign in again after a rebuild.
The local container selects `AzureCliCredential` to avoid managed-identity probing.
Optional GitHub operations use `gh auth login`.

On ARM64 machines SQL Server still needs x86-64 emulation, subject to the same
support limitations below. The optional topology pipeline examples additionally
require `rabbitmqadmin v2` or `kubectl` when targeting those platforms; neither is
needed for the demos or exercises and neither is installed by this container.

### Host setup

Install Docker Desktop and the .NET 10 SDK. Run these commands from the repository root
in PowerShell, Command Prompt or a Unix shell:

```bash
cd infra
docker compose up -d       # RabbitMQ (5673/15673), Service Bus emulator (5672/5300), SQL Server (1433)
# then run infra/sql-init.sql against SQL Server for the Reliability Inbox/Outbox tables
```

- **Windows x64:** use Docker Desktop's Linux-container mode with the WSL 2 backend
  (enable WSL 2 and hardware virtualization). SQL Server runs natively as `linux/amd64`;
  no Compose changes are needed. Do not switch Docker Desktop to Windows containers.
- **Apple Silicon / Windows ARM64:** SQL Server requires x86-64 emulation in Docker
  Desktop; the Service Bus emulator and RabbitMQ use their native images. Microsoft
  does not support SQL Server under emulation. Use an x86-64 Linux host if emulation
  is unavailable or unreliable.

Check startup with `docker compose ps` from `infra/`. All three services should be running.
The emulator configuration uses duplicate-detection windows of at most five minutes,
which is the emulator's limit.
Rebus disables management operations against the emulator, so
`infra/servicebus-emulator-config.json` predefines its queues and event subscriptions
for M2 and Exercises 2/3. Subscriptions forward events to each Rebus input queue.
After changing that file, restart the emulator and wait for its health endpoint:

```bash
docker compose -f infra/docker-compose.yml restart servicebus-emulator
curl --fail http://localhost:5300/health
```

Run these commands from the repository root, with the local Service Bus demos stopped.
The emulator can take about a minute to become healthy after restarting.
Wolverine provisions its emulator entities through the management endpoint.
Its exercises use the same quorum system dead-letter queue as M3, avoiding
queue-type conflicts when switching between modules without deleting queues.

Connection strings are in each project's `appsettings.json`:

* Service Bus emulator: `Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;`
* RabbitMQ: `amqp://guest:guest@localhost:5673/` (management UI on http://localhost:15673)
* SQL Server: `Server=localhost,1433;User Id=sa;Password=Training#2026;TrustServerCertificate=true;Database=Messaging`

Each module is a console app (`dotnet run --project src/M2.Rebus`).
Demo order in class: M1 (slide 17); M2/M3 menus 2 + 1 (Payments and Orders, two terminals) and the broker switch (slides 28–29); Exercise 2 (slide 30); M4 at slide 41, whose run you show again in Application Insights at slide 49; Exercise 3 (slide 42); M5 sagas (slides 56–59). Each demo only uses what the slides have shown so far.

## Debugging in VS Code

Open the repository root in VS Code and install the Microsoft C# extension
(`ms-dotnettools.csharp`; C# Dev Kit is optional). Select a configuration in
**Run and Debug**, set breakpoints and press **F5**. The configuration builds only
its own project in Debug mode, so an unfinished starter does not block a solution.
Enter menu choices and demo keys in the integrated terminal, not the Debug Console.

- M1, M2, M3, both M5 modes, and every Ex2/Ex3 starter and solution have configurations.
- For M2, M3 and Ex2, start one session and choose menu **2**, then use **Run > Start
  Debugging** with the same configuration to start a second session and choose **1**.
- **M4 - Orders + Payments** launches both processes in separate terminals. Stop or
  restart either process independently for the crash scenarios; stopping one does
  not stop the other. M4 still needs SQL Server and its normal telemetry setup.
- Broker configurations use the existing `broker.json` / `BROKER` setting.
  To override with an argument, set `"args": ["asb"]` or `["rabbit"]` on the relevant
  configuration in `.vscode/launch.json`; use the same broker for both processes.
- Ex1 and Ex4 are paper/whiteboard exercises, so there is no program to debug.

The launch paths and build tasks work on Windows, macOS and Linux; Docker prerequisites
remain as described above.

## One broker setting for every framework demo

`broker.json` in the repository root decides the broker for **M2.Rebus, M3.Wolverine, M4.ReliableOrders and Exercises 2 + 3**:

```json
{ "Broker": "rabbit" }      // or "asb"
```

Override per run without editing the file: `dotnet run -- asb`, `--Broker asb` or `BROKER=asb`.
Everything that differs between Service Bus and RabbitMQ is in **one file per framework**:

| Project | Broker-specific file | What is in it |
|---|---|---|
| `src/M2.Rebus` | `Transport.cs` | the transport line |
| `src/M3.Wolverine` | `Transport.cs` | the transport line, command queue, event topic/exchange |
| `src/M4.ReliableOrders` | `M4.Shared/Transport.cs` | the transport line, the lock duration (Service Bus), the timeout store for delayed retries (RabbitMQ only) |
| `exercises/Ex2.FirstEndpoint` | `Shared/Rebus/Plumbing.cs`, `Shared/Wolverine/Plumbing.cs` | the transport line (+ queue and topic helpers for Wolverine) |
| `exercises/Ex3.ReliableConsumer` | `Shared/Rebus/ExerciseTransport.cs`, `Shared/Wolverine/ExerciseTransport.cs` | the transport line (+ queue route for Wolverine) |

What a setting cannot hide (and the demos say so on screen): where dead letters end up, delayed retries
(native scheduling on Service Bus, a timeout store on RabbitMQ) and RabbitMQ queue arguments. M4 runs on either
broker (best on Azure Service Bus with Application Insights); M5 (sagas) is in-memory.

Suggested live moment: run `M2.Rebus` menu 2 (Payments) and menu 1 (Orders) on RabbitMQ, change `broker.json` to
`asb` (or rerun both with `-- asb`), and send an order again. Same code, other broker.

## Important notes for the trainer

* Package versions are pinned centrally in `Directory.Packages.props`. Build with the .NET 10 SDK
  before the session. A successful build does not replace the broker and telemetry dry runs.
  If the NuGet v3 feed is unreachable, restore with
  `dotnet restore --source https://www.nuget.org/api/v2/`, then build with `dotnet build --no-restore`.
* Every Wolverine project references `WolverineFx.RuntimeCompilation`, matching the Wolverine
  version. Dynamic handler compilation requires this separate package; a successful build
  alone does not detect its absence.
* The Service Bus **emulator** is for dev/test only, caps messages at 256 KB and does not
  support every cloud feature — show Premium-only features (large messages, geo-replication)
  on slides or a real namespace.
* M2 and M3 are deliberately minimal: two processes and one command/event round trip. Retries, dead letters,
  the outbox and sagas come later (M4, Exercise 3, M5); ordering is on the slides only.

## M4 – reliable orders, then the same run in Application Insights

See `src/M4.ReliableOrders/DEMO.md`. In short: `./setup.sh` the day before (Application Insights + a Service Bus
namespace), `docker compose up -d` for SQL Server, `./run-demo.sh asb`, then run its printed commands
in two Bash terminals and drive it with keys from the orders-api
window at slide 41. At slide 49 open Application Insights and walk through the orders you placed: no second run needed.
M4 suppresses successful background outbox-polling SQL spans, keeps failures and business
SQL, uses stable local role-instance IDs, and collects Service Bus SDK spans alongside
readable Rebus spans. For a clean map, select a time range beginning after startup;
see the runbook for details and telemetry checks.
The scripts require Bash (macOS/Linux or Git Bash on Windows); setup also needs Python 3 and Azure CLI.
For WSL, install .NET, Python and Azure CLI inside WSL and enable Docker Desktop's WSL integration.

## Topology as code

See `topology/README.md`: reusable Bicep modules for Service Bus, Topology Operator YAML and a rabbitmqadmin pipeline script for RabbitMQ (self-hosted and managed), a ready-to-copy Git + CI/CD layout for the shared bus (`topology/cicd`), and Rebus/Wolverine snippets that pin names, set defaults explicitly and only provision topology in dev.

## Framework demos (M2, M3, M4, Exercises 2 + 3) – check before the session

Verify the pinned packages with `dotnet build` and one dry run before the session:

* Rebus traces: `EnableDiagnosticSources()` + `AddRebusInstrumentation()` (Rebus.Diagnostics / Rebus.OpenTelemetry). Span names depend on the version.
* M4: the handler uses the Rebus outbox's SQL connection and transaction (`OutboxConnection` under
  `"current-outbox-connection"` in `SameTransaction.cs`).
* M4: `SetMessagePeekLockDuration(30 s)` on Service Bus, so a killed consumer's message comes back quickly. Check the method name in your Rebus.AzureServiceBus version.
* M4 'x' (crash after commit): if the outbox happens to send before the process dies, run it again – the point is that the
  row is in the outbox table before anything is sent.
* Both projects disable handler auto-discovery per menu item (Wolverine `Discovery.DisableConventionalDiscovery().IncludeType(...)`,
  Rebus explicit `AddRebusHandler<T>()`), so the menu items don't pick up each other's handlers.
* `broker.json` uses comments; Microsoft.Extensions.Configuration's JSON provider accepts them.
