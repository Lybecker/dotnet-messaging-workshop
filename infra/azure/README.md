# Azure environment for the demos (Bicep, Entra ID only)

`main.bicep` creates everything the Service Bus / SQL demos need, with **no keys or passwords**:

| Resource | Purpose | Access |
|---|---|---|
| User-assigned managed identity `msgtrain-demo-id` | Identity of the demo apps when they run in Azure | — |
| Service Bus namespace (Standard, local auth disabled) | queues `payments`, `orders-rebus`; topic `order-events` + subscriptions `billing`, `shipping`, `analytics`, `orders-wolverine` | Managed identity **and** you: *Azure Service Bus Data Owner* (or Sender + Receiver with `leastPrivilege=true`) |
| Azure SQL server + DB `Messaging` (Entra-only auth) | Wolverine/Rebus outbox, inbox, sagas; Reliability Inbox/Outbox | You = Entra admin; managed identity = DB user via `sql-grant-identity.sql` |
| *(optional)* RabbitMQ in Container Instances | RabbitMQ demos without local Docker | username/password (RabbitMQ has no built-in Entra ID support) |

## Deploy

```bash
az login
cd infra/azure
./deploy.sh rg-messaging-training westeurope            # add --with-rabbitmq for the ACI broker
# then, once, as yourself:
sqlcmd -S <sqlServerFqdn> -d Messaging --authentication-method ActiveDirectoryDefault -i sql-grant-identity.sql
```

Or directly:

```bash
az deployment group create -g rg-messaging-training -f main.bicep \
  -p trainerPrincipalId=$(az ad signed-in-user show --query id -o tsv) \
     trainerPrincipalName=$(az ad signed-in-user show --query userPrincipalName -o tsv)
```

## Run the samples against Azure

```bash
export ASB_NAMESPACE="<output ASB_NAMESPACE>"     # switches the samples from emulator to Azure
export SQL_CONNECTION="<output SQL_CONNECTION>"   # Authentication=Active Directory Default
dotnet run --project ../../src/M3.Wolverine -- asb
dotnet run --project ../../src/M2.Rebus -- asb
```

* **On your laptop** leave `AZURE_CLIENT_ID` unset: `DefaultAzureCredential` uses your `az login`, which the template granted the same roles.
* **In Azure** (Container Apps, App Service, AKS workload identity, VM) attach `msgtrain-demo-id` to the compute and set `AZURE_CLIENT_ID` to the output value – the identical code then authenticates as the managed identity.
* Role assignments can take a few minutes to propagate; a `401/Unauthorized` right after deployment usually resolves itself.

## Notes for the "governance" discussion

* `disableLocalAuth: true` removes SAS keys entirely – show that the portal's connection strings no longer work.
* *Data Owner* is a **demo** shortcut so Wolverine/Rebus/`ServiceBusAdministrationClient` can create entities.
  Production: deploy topology with IaC (this file) and give apps *Data Sender* / *Data Receiver* scoped to the
  specific queue/topic/subscription (`leastPrivilege=true` shows the namespace-level variant).
* Subscription filters are deployed as rule **`$Default`** on purpose: an additional rule would be OR-ed with the
  implicit TrueFilter and let every message through.
* Standard tier is enough for every demo. Premium is needed for >256 KB messages, geo-replication, private endpoints, CMK.
* Clean up: `az group delete -n rg-messaging-training`.

> Not validated with `az bicep build` in the authoring environment – run `az bicep build -f main.bicep` (or a
> `what-if` deployment) once before the training.
