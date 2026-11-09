// M5 · Sagas with Rebus – the same order flow, run two ways. In-memory: no broker, no database.
// Purpose: orchestration vs choreography with the same failure: the second order's card is declined, so you
//          see the compensation (ReleaseStock) and the cancellation.
// Show:    Orchestration.cs – one saga knows the whole flow. Choreography.cs – only events, no coordinator;
//          ask: where would you look to see the whole flow? In production only the Transport(...) line changes.
// Run:     dotnet run -- orchestration (default) | dotnet run -- choreography
using Contoso.Messaging.Contracts;
using M5.Sagas;
using Choreography = M5.Sagas.Choreography;
using Orchestration = M5.Sagas.Orchestration;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Persistence.InMem;
using Rebus.Routing.TypeBased;
using Rebus.Transport.InMem;

var mode = args.FirstOrDefault() ?? "orchestration";
var network = new InMemNetwork();                     // stands in for the broker
var subscriberStore = new InMemorySubscriberStore();  // stands in for topics/exchanges
var activators = new List<BuiltinHandlerActivator>();

IBus StartService(string queue, Action<BuiltinHandlerActivator> registerHandlers, bool withSagas = false)
{
    var activator = new BuiltinHandlerActivator();
    activators.Add(activator);
    registerHandlers(activator);
    var configurer = Configure.With(activator)
        .Logging(l => l.None())
        .Transport(t => t.UseInMemoryTransport(network, queue, registerSubscriptionStorage: false))
        .Subscriptions(s => s.StoreInMemory(subscriberStore))
        .Routing(r => r.TypeBased()                    // commands go to ONE owning queue
            .Map<ReserveStock>("inventory")
            .Map<ReleaseStock>("inventory")
            .Map<ChargePayment>("payment"));
    if (withSagas)
        configurer = configurer
            .Sagas(s => s.StoreInMemory())             // production: StoreInSqlServer / PostgreSQL / …
            .Timeouts(t => t.StoreInMemory());         // for DeferLocal; ASB has native scheduling
    return configurer.Start();
}

IBus orders;
if (mode == "choreography")
{
    Console.WriteLine("CHOREOGRAPHY: nobody coordinates, every service reacts to events.");
    orders = StartService("orders", a => a.Register((bus, _) => new Choreography.OrdersHandlers(bus)));
    var inventory = StartService("inventory", a => a.Register((bus, _) => new Choreography.InventoryHandlers(bus)));
    var payment = StartService("payment", a => a.Register((bus, _) => new Choreography.PaymentHandlers(bus)));

    // Each service declares, in code, the events it listens to.
    // ASB: Rebus creates a topic per event type + a subscription; RabbitMQ: a binding to the event's topic exchange.
    await inventory.Subscribe<OrderPlaced>();
    await inventory.Subscribe<PaymentFailed>();
    await payment.Subscribe<StockReserved>();
    await orders.Subscribe<StockReleased>();
    await orders.Subscribe<PaymentCharged>();
}
else
{
    Console.WriteLine("ORCHESTRATION: the OrderSaga sends commands and reacts to replies.");
    orders = StartService("orders", a => a.Register((bus, _) => new Orchestration.OrderSaga(bus)), withSagas: true);
    StartService("inventory", a => a.Register((bus, _) => new Orchestration.InventoryCommandHandlers(bus)));
    StartService("payment", a => a.Register((bus, _) => new Orchestration.PaymentCommandHandlers(bus)));
    await orders.Subscribe<OrderPlaced>();         // the saga starts on the OrderPlaced event
}

foreach (var declined in new[] { false, true })    // happy path, then a declined card
{
    var id = Guid.NewGuid();
    if (declined) Cards.Decline(id);
    Console.WriteLine($"\n=== Order {id.ToString()[..8]}{(declined ? "  (card will be declined)" : "")} ===");
    await orders.Publish(new OrderPlaced(id, Guid.NewGuid(), [new OrderLine("SKU-1", 1, 99m)], 99m, "EUR",
        new Address("Main St 1", "Lyngby", "2800", "DK"), DateTimeOffset.UtcNow));
    await Task.Delay(1500);
}

activators.ForEach(a => a.Dispose());
