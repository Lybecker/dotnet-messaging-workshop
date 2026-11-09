# Exercise 4 – Design your topology (whiteboard)

**When:** in "Operations & platform", after "Topology as code, owned by the consumer"
**Time:** 15 min (10 min in pairs + 5 min: two pairs present for 2 minutes each) · **No code**

Pick ONE real flow from your own landscape (fallback below). Sketch it on one page.

## Fallback scenario

Orders, Payments, Warehouse, Notifications and Analytics, owned by **three teams**.
Today there are about a dozen Service Bus namespaces, created by hand. Subscriptions are added
in the portal, and a deployment last month broke the Warehouse consumer.

## Answer on your sheet

1. **One bus or several?** One shared namespace/vhost, one per team, or a hybrid?
   Argue with criteria: blast radius, quotas/throughput, ownership, environments, network, cost.
2. **Entities.** Which queues (commands) and topics/exchanges (events)? Who **owns** each one?
3. **Subscriptions.** For each consumer: which subscription/binding, which filter, and **who creates
   and deploys it** – the publisher, the consumer, or a platform team? In code, IaC or the portal?
4. **Request–reply.** Is there a flow that needs an answer? Reply queue per instance, sessions, or
   is it really a query that should stay HTTP?
5. **A breaking change** to `OrderPlaced` is coming. Walk it through: expand, migrate, contract.
   What in your design makes that easy or hard?

## What good looks like

* Every queue, topic and subscription has exactly one owner.
* Consumers own their subscriptions, deployed with the consumer's pipeline (IaC), not by hand.
* The number of namespaces follows a criterion you can name, not history.
