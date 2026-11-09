using Contoso.Messaging.Contracts;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace M5.Sagas.Orchestration;

// ORCHESTRATION: one component, the saga, knows the whole flow. The numbers match the slide:
//   1 ReserveStock → 2 StockReserved → 3 ChargePayment → 4 PaymentFailed → 5 ReleaseStock (undo) → OrderCancelled
public class OrderSaga(IBus bus) : Saga<OrderSagaData>, IAmInitiatedBy<OrderPlaced>,
    IHandleMessages<StockReserved>, IHandleMessages<PaymentFailed>, IHandleMessages<PaymentCharged>,
    IHandleMessages<OrderTimeout>
{
    protected override void CorrelateMessages(ICorrelationConfig<OrderSagaData> c)
    {
        // Every message must say which saga instance it belongs to: here, the OrderId
        c.Correlate<OrderPlaced>(m => m.OrderId, d => d.OrderId);
        c.Correlate<StockReserved>(m => m.OrderId, d => d.OrderId);
        c.Correlate<PaymentFailed>(m => m.OrderId, d => d.OrderId);
        c.Correlate<PaymentCharged>(m => m.OrderId, d => d.OrderId);
        c.Correlate<OrderTimeout>(m => m.OrderId, d => d.OrderId);
    }

    public async Task Handle(OrderPlaced m)
    {
        if (!IsNew) return;                                          // duplicate OrderPlaced → ignore
        Data.OrderId = m.OrderId;
        Data.CustomerId = m.CustomerId;
        Data.Total = m.Total;
        Data.Currency = m.Currency;
        Console.WriteLine($"  [saga] 1 send ReserveStock");
        await bus.Send(new ReserveStock(m.OrderId, m.Lines));                        // 1 command
        await bus.DeferLocal(TimeSpan.FromMinutes(15), new OrderTimeout(m.OrderId)); // the saga owns the deadline
    }

    public async Task Handle(StockReserved m)                                       // 2 reply
    {
        Data.StockReserved = true;
        Console.WriteLine($"  [saga] 2 got StockReserved → 3 send ChargePayment");
        await bus.Send(new ChargePayment(m.OrderId, Data.CustomerId, Data.Total, Data.Currency)); // 3 command
    }

    public async Task Handle(PaymentFailed m)                                       // 4 reply ✗
    {
        Console.WriteLine($"  [saga] 4 got PaymentFailed → 5 send ReleaseStock (compensate)");
        await bus.Send(new ReleaseStock(m.OrderId, m.Reason));                     // 5 undo step 1
        await bus.Publish(new OrderCancelled(m.OrderId, m.Reason));
        MarkAsComplete();                                                          // saga state is deleted
    }

    public async Task Handle(PaymentCharged m)
    {
        Console.WriteLine($"  [saga] got PaymentCharged → OrderCompleted (shipping left out to keep it short)");
        await bus.Publish(new OrderCompleted(m.OrderId));
        MarkAsComplete();
    }

    public async Task Handle(OrderTimeout m)
    {
        // Nothing answered in 15 minutes: undo what was done, then cancel
        if (Data.StockReserved) await bus.Send(new ReleaseStock(m.OrderId, "Timed out"));
        await bus.Publish(new OrderCancelled(m.OrderId, "Timed out"));
        MarkAsComplete();
    }
}

public class OrderSagaData : SagaData      // SagaData adds Id + Revision (optimistic concurrency)
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "";
    public bool StockReserved { get; set; }
}

public record OrderTimeout(Guid OrderId);

// The services know nothing about the flow: they handle a command and reply.
public class InventoryCommandHandlers(IBus bus) : IHandleMessages<ReserveStock>, IHandleMessages<ReleaseStock>
{
    public async Task Handle(ReserveStock cmd)
    {
        Stock.Reserve(cmd.OrderId);
        await bus.Reply(new StockReserved(cmd.OrderId));             // reply goes back to the saga
    }

    public Task Handle(ReleaseStock cmd)                             // compensation: idempotent, safe to repeat
    {
        Stock.Release(cmd.OrderId);
        return Task.CompletedTask;
    }
}

public class PaymentCommandHandlers(IBus bus) : IHandleMessages<ChargePayment>
{
    public async Task Handle(ChargePayment cmd)
    {
        Console.WriteLine($"  [payment] charge {cmd.Amount} {cmd.Currency}");
        if (Cards.TryCharge(cmd.OrderId))
            await bus.Reply(new PaymentCharged(cmd.OrderId, "PAY-1", cmd.Amount));
        else
            await bus.Reply(new PaymentFailed(cmd.OrderId, "Card declined", IsPermanent: true));
    }
}
