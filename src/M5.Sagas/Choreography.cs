using Contoso.Messaging.Contracts;
using Rebus.Bus;
using Rebus.Handlers;

namespace M5.Sagas.Choreography;

// CHOREOGRAPHY: no coordinator. Every arrow is an event; the numbers match the slide:
//   1 OrderPlaced → 2 StockReserved → 3 PaymentFailed → 4 StockReleased → OrderCancelled
// Each class below would live in a different service. Subscriptions are declared in Program.cs.

public class InventoryHandlers(IBus bus) : IHandleMessages<OrderPlaced>, IHandleMessages<PaymentFailed>
{
    public async Task Handle(OrderPlaced e)                          // reacts to 1
    {
        Stock.Reserve(e.OrderId);
        await bus.Publish(new StockReserved(e.OrderId));             // 2
    }

    public async Task Handle(PaymentFailed e)                        // reacts to 3
    {
        Stock.Release(e.OrderId);                                    // compensation, triggered by an EVENT
        await bus.Publish(new StockReleased(e.OrderId));             // 4
    }
}

public class PaymentHandlers(IBus bus) : IHandleMessages<StockReserved>
{
    public async Task Handle(StockReserved e)                        // reacts to 2
    {
        Console.WriteLine($"  [payment] charge {Stock.Short(e.OrderId)}");
        object result = Cards.TryCharge(e.OrderId)
            ? new PaymentCharged(e.OrderId, "PAY-1", 99m)
            : new PaymentFailed(e.OrderId, "Declined", true);   // 3
        await bus.Publish(result);
    }
}

public class OrdersHandlers(IBus bus) : IHandleMessages<StockReleased>, IHandleMessages<PaymentCharged>
{
    public async Task Handle(StockReleased e)                        // reacts to 4
    {
        Console.WriteLine($"  [orders] CANCELLED {Stock.Short(e.OrderId)}");
        await bus.Publish(new OrderCancelled(
            e.OrderId, "Declined"));
    }

    public async Task Handle(PaymentCharged e)                       // happy path (shipping left out)
    {
        Console.WriteLine($"  [orders] COMPLETED {Stock.Short(e.OrderId)}");
        await bus.Publish(new OrderCompleted(e.OrderId));
    }
}
