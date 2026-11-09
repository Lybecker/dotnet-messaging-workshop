# Exercise 3 – Make a flaky consumer reliable (Rebus or Wolverine)

**When:** end of Reliability, slide 42 · **Time:** 20 min (15 min hands-on + 5 min debrief) · **Pairs welcome**

Keep the framework you picked in Exercise 2. Both starters have the same TODOs, so the debrief can compare them.
Tasks 1 and 2 are the moves you already made in Exercise 2; the new part is tasks 3 and 4.

| Folder | What |
|---|---|
| `Rebus.Starter` / `Rebus.Solution` | Rebus 8 – explicit `IBus`, `Send`/`Publish`, `IFailed<T>` second-level retries |
| `Wolverine.Starter` / `Wolverine.Solution` | Wolverine – convention handlers, cascading return values, declarative error policies |
| `Shared` | `FakePaymentGateway` (do not change it), the console menu, and `Rebus/` + `Wolverine/ExerciseTransport.cs` – the only broker-specific code, already written |

## Before you start

```bash
cd infra && docker compose up -d          # RabbitMQ on 5673 (UI http://localhost:15673), Service Bus emulator on 5672
cd ../exercises/Ex3.ReliableConsumer/Rebus.Starter       # or Wolverine.Starter
dotnet run                                 # the broker in broker.json (repository root)
dotnet run -- asb                          # or override: Service Bus emulator · dotnet run -- rabbit → RabbitMQ
```

**Pick your broker with one setting.** `broker.json` in the repository root says `"Broker": "rabbit"` or `"asb"`.
You never write broker code in this exercise: the transport is already wired, and every TODO is the same on
Service Bus and RabbitMQ. Finished early? Switch the broker and run your solution again – nothing else changes.

## The gateway fails in three different ways

| Failure | Kind | What SHOULD happen |
|---|---|---|
| `GatewayTimeoutException` (~40% of calls) | Transient | Retry quickly, then later with back-off, then error queue |
| `CardDeclinedException` (amount > 1000) | Business outcome | No retry. Publish `PaymentFailed` |
| `UnsupportedCurrencyException` (currency `XXX`) | Permanent / poison | No (more) retries. Straight to the error queue |

## Tasks

1. **Route the command through the broker.** A command has one owner: Rebus `Map<ChargePayment>(queue)`;
   Wolverine `ExerciseTransport.CommandQueue<ChargePayment>(opts, broker, queue)`.
2. **Handle the command.** Call the gateway, publish/return `PaymentCharged`.
3. **Retries and the error queue.** Make timeouts retry with back-off; make poison messages fail fast.
   Press **b** (burst) and **p** (poison), then find the poison message in the RabbitMQ UI.
4. **Business failure.** Press **d**. A declined card must become a `PaymentFailed` event, not a retry storm.

**Stretch:** press **r** – the same command arrives twice. Fix the red *DOUBLE CHARGE*. Why does
deduplication on the message id not help here?
The solution serializes the business-key check and charge within one process; a check without
synchronization races when two handlers run concurrently. This is not durable, cross-process
idempotency: production requires an idempotency key at the gateway.

## Debrief questions

* Where did the poison message end up, and who would get alerted in production?
* Rebus vs Wolverine: where does the retry policy live? Which one would your team rather read in a year?
* What would this have taken with the native `ServiceBusProcessor`?
* You switched brokers by changing one setting. What did NOT carry over (error queue location, queue types)?

The starter and solution share project configuration, plumbing and handler skeletons.
TODOs remain intentionally unsolved in the starter. Both variants build with the pinned packages;
unused-parameter warnings in starter skeletons are expected. Run the solutions against each broker
before the session.

> Error queue names: Rebus uses `error` (as configured); Wolverine's RabbitMQ default is
> `wolverine-dead-letter-queue` (check the name for your Wolverine version). On the Service Bus
> emulator Rebus still uses its configured `error` queue; Wolverine uses the queue's built-in
> dead-letter sub-queue.
