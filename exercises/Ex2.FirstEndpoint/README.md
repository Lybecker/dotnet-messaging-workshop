# Exercise 2 – Your first endpoint (Rebus or Wolverine)

**When:** end of Messaging frameworks, slide 30 · **Time:** 15 min (10 min hands-on + 5 min debrief) · **Pairs welcome**

Pick ONE framework. Both starters have the same four TODOs, so the debrief can compare them.
You have just seen M2/M3 do this with Payments; now you do it yourself with Shipping.

```
Orders ──ShipOrder (command, Send)──▶ [shipping queue] ──▶ Shipping
Orders ◀──OrderShipped (event, Publish)── [topic / exchange] ◀── Shipping
```

| Folder | What |
|---|---|
| `Rebus.Starter` / `Rebus.Solution` | Rebus – explicit `IBus`: `Send`, `Publish`, `Subscribe`, type-based routing |
| `Wolverine.Starter` / `Wolverine.Solution` | Wolverine – convention handlers; the returned message is published for you |
| `Shared` | `Rebus/` + `Wolverine/Plumbing.cs`: broker connection and hosting, already written – the only broker-specific code |

## Before you start

```bash
cd infra && docker compose up -d                        # RabbitMQ on 5673 (UI http://localhost:15673), Service Bus emulator on 5672
cd ../exercises/Ex2.FirstEndpoint/Rebus.Starter         # or Wolverine.Starter
dotnet run            # terminal 1: pick 2 (Shipping)
dotnet run            # terminal 2: pick 1 (Orders), then press ENTER
```

The broker comes from `broker.json` in the repository root (`"rabbit"` or `"asb"`), or per run: `dotnet run -- asb`.

## Tasks (Orders.cs and Shipping.cs)

1. **Route the command.** `ShipOrder` has one owner: Shipping. Tell Orders which queue that is.
2. **Send it.** On ENTER, send `ShipOrder`. (Try it once before task 1: what happens without a route?)
3. **Handle it and publish the event.** Shipping prints the order and publishes `OrderShipped` with a tracking number.
4. **Subscribe.** Orders subscribes to `OrderShipped` and prints it.

Done when Orders prints `← OrderShipped … (TRK-…)` for every order you send.

**Stretch (pick one)**

* Switch the broker in `broker.json`, restart both terminals and send again. What did you change in your code?
* Start a second Shipping terminal. Who gets each command? Now start a second Orders terminal: who gets each event?
* Open the RabbitMQ UI (or Service Bus Explorer): find your queue, the exchange / topic and a message's headers.

## Debrief questions

* Rebus vs Wolverine: where does the route live, and where does the event get published? Which would your team rather read?
* What did the framework do for you that the native SDK slide made you do by hand (serialisation, headers, settle)?
* Why does a command need a route, but an event does not?

The starter and solution share the same project configuration and plumbing. Differences are limited
to the numbered tasks and starter/solution guidance; TODOs remain intentionally unsolved in the starter.
Both variants build with the pinned packages. Run both solutions against each broker before the session.
