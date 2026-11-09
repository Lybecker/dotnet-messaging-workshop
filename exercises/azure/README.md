# Exercises without Docker: Azure Service Bus

Exercises 2 and 3 need only a broker, not SQL Server or Application Insights.
This template creates one **Standard** Service Bus namespace (topics require
Standard) and grants the attendee **Azure Service Bus Data Owner** on that namespace.
SAS authentication is disabled; the applications use Entra ID.

The frameworks provision their queues, topics, subscriptions and internal retry
entities as needed. Data Owner is deliberately scoped to this training namespace;
production applications should use narrower permissions and pre-provisioned topology.
The same setup serves Rebus and Wolverine starters and solutions without changing
the attendee TODOs.

## Prerequisites and cost

- .NET 10 SDK and Azure CLI with Bicep (`az bicep install`).
- An Azure subscription and permission to create resources **and role assignments**.
  Contributor alone cannot grant roles; ask the trainer/admin if necessary.
- Network access to Service Bus over AMQP TLS (5671) and HTTPS (443).

**Standard Service Bus incurs charges while provisioned**, even without traffic.
Use a dedicated resource group and namespace per attendee: the exercises use fixed
queue names, so sharing a namespace makes attendees compete for messages.
This setup replaces Docker for the exercises, not for M4's SQL database.
RabbitMQ is not deployed; select `asb`. Paper exercises 1 and 4 need no Azure resources.

## Deploy once (Bash, from the repository root)

Use Bash on macOS/Linux, Git Bash on Windows, or WSL. Choose your own group name
and a region where Service Bus Standard is available.

```bash
az login --use-device-code
az account set --subscription "<subscription-name-or-id>"

RG="rg-messaging-exercises-yourname"
LOCATION="westeurope"
ATTENDEE_ID="$(az ad signed-in-user show --query id --output tsv)"

az group create --name "$RG" --location "$LOCATION"
az deployment group create \
  --resource-group "$RG" \
  --name exercises \
  --template-file exercises/azure/main.bicep \
  --parameters attendeePrincipalId="$ATTENDEE_ID"
```

An administrator can run the deployment on your behalf using your user object ID.
No connection string or secret is generated.

## Configure every exercise terminal

After signing in with `az login` in that environment, run these exports in **each**
terminal (including both Orders and Shipping terminals for Exercise 2):

```bash
RG="rg-messaging-exercises-yourname"
export ASB_NAMESPACE="$(az deployment group show \
  --resource-group "$RG" --name exercises \
  --query properties.outputs.serviceBusNamespace.value --output tsv)"
export AZURE_TOKEN_CREDENTIALS=AzureCliCredential
```

Do not set `AZURE_CLIENT_ID` for local user sign-in. These variables are scoped to
the shell; they are not saved to the repository. For VS Code debugging, open VS Code
from this configured terminal or set the same variables in your debug environment.
Allow several minutes for a new role assignment to propagate.

## Run

From the repository root, for example:

```bash
# Exercise 2: two terminals with the same exports; Shipping (2), then Orders (1)
dotnet run --project exercises/Ex2.FirstEndpoint/Rebus.Starter -- asb

# Exercise 3: one terminal
dotnet run --project exercises/Ex3.ReliableConsumer/Rebus.Starter -- asb
```

Use `Wolverine.Starter` instead to choose Wolverine. Trainer checks use
`Rebus.Solution` / `Wolverine.Solution`. Complete the same TODOs described in the
exercise READMEs; starters intentionally do not implement the complete flow.

- Exercise 2 solution: each Enter produces `Shipping ...` and `OrderShipped ...`.
- Exercise 3 solution: normal payment eventually succeeds, `r` does not double
  charge, `d` yields PaymentFailed, and `p` goes to the framework's error destination.
- For a 401/403, check the signed-in account, namespace and role propagation.
- RabbitMQ's local management UI is not available in this mode. Inspect queues,
  topics and error destinations in Azure Portal's Service Bus Explorer instead.

## Return to local mode and clean up

```bash
unset ASB_NAMESPACE AZURE_TOKEN_CREDENTIALS
```

`asb` now selects the local emulator again; `rabbit` selects the local RabbitMQ.
These options require Docker or another accessible broker.

After the workshop, delete **only your dedicated exercise group**:

```bash
az group delete --name rg-messaging-exercises-yourname --yes --no-wait
```

Deletion is asynchronous and removes everything in that resource group.
