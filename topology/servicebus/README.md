# Service Bus topology as code (Bicep)

Reusable modules so every team declares its own topology the same way.

| Module | Owned by | Creates | Data-plane role it grants |
|---|---|---|---|
| `modules/topic.bicep` | publisher | one topic (`<domain>.<event>.v<major>`) | Data Sender → publisher |
| `modules/subscription.bicep` | consumer | one subscription (+ optional SQL filter, optional forwardTo) | Data Receiver → consumer |
| `modules/queue.bicep` | receiver | one queue (commands, or a Rebus input queue) | Data Receiver → owner, Data Sender → allowed senders |

Defaults are explicit: `maxDeliveryCount 10`, `lockDuration PT1M`, `defaultMessageTimeToLive P14D`,
dead-letter on expiry. `maxDeliveryCount` must be higher than the retries your framework performs itself.

## Deploy as a deployment stack (so removals really remove)

A normal (incremental) deployment never deletes a subscription you removed from the file. A deployment
stack does:

```bash
az stack group create \
  --name billing-topology \
  --resource-group rg-messaging-prod \
  --template-file examples/billing.subscriptions.bicep \
  --parameters namespaceName=sb-contoso-prod billingPrincipalId=<object-id> \
  --action-on-unmanage deleteResources \
  --deny-settings-mode none
```

Run the same command for test and prod (they are alike); only the parameters differ.

## Who needs which rights

* **Pipeline identity (control plane):** Contributor (or a custom "Service Bus entity writer" role) on the
  namespace, plus *Role Based Access Control Administrator* constrained to the two Service Bus data roles
  so it can create the role assignments.
* **Application identity (data plane):** only the Data Sender / Data Receiver assignments made above.
  Never *Azure Service Bus Data Owner* outside dev.

## Share the modules

```bash
az bicep publish --file modules/subscription.bicep \
  --target br:<acr>.azurecr.io/bicep/servicebus/subscription:1.0.0
```
Then reference `br:<acr>.azurecr.io/bicep/servicebus/subscription:1.0.0` instead of the relative path.

Verify API versions and role ids against Microsoft Learn before use.
