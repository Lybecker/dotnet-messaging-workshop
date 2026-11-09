# Exercises

| # | When | Exercise | Type | Time |
|---|---|---|---|---|
| 1 | Core principles of EDA (slide 18) | Command, event or query? (slide only) | Pairs, paper | 10 min |
| 2 | End of Messaging frameworks (slide 30) | [Your first endpoint – Rebus or Wolverine](Ex2.FirstEndpoint/README.md) | Code | 15 min |
| 3 | End of Reliability (slide 42) | [Make a flaky consumer reliable – Rebus or Wolverine](Ex3.ReliableConsumer/README.md) | Code | 20 min |
| 4 | Operations & platform (slide 69) | [Design your topology](Ex4.Topology/README.md) | Pairs, whiteboard | 15 min |

Exercises 2 and 3 use the same framework choice: whoever picked Rebus or Wolverine in Exercise 2 keeps it in Exercise 3.

**No Docker?** Use the [Azure Service Bus exercise setup](azure/README.md).
It includes a Bicep template, Entra ID permissions and commands for both frameworks.
Each attendee needs a separate namespace; Azure resources incur charges.
