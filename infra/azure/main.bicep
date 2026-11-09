// ─────────────────────────────────────────────────────────────────────────────────────────────
// Azure environment for the "Event- & Message-Driven Solutions" training demos.
//
// Deploys (resource-group scope):
//   * User-assigned managed identity for the demo apps
//   * Azure Service Bus namespace (Standard by default, local/SAS auth DISABLED → Entra ID only)
//       queues : payments (dup detection), orders-rebus
//       topic  : order-events → subscriptions billing (correlation filter), shipping (SQL filter),
//                analytics (all), orders-wolverine (all)
//   * Azure SQL logical server (Entra-only auth, you are the Entra admin) + database "Messaging"
//     for the Wolverine/Rebus inbox/outbox/saga tables and the Reliability Inbox/Outbox demo
//   * OPTIONAL: RabbitMQ 4 in Azure Container Instances (RabbitMQ has no Entra ID / managed identity
//     support out of the box → username/password; demo use only)
//
// Access (Azure RBAC, no keys):
//   * Managed identity + trainer user → "Azure Service Bus Data Owner" on the namespace
//     (demo convenience: Wolverine/Rebus/AdminClient create entities). Set leastPrivilege=true to grant
//     only Data Sender + Data Receiver instead (then disable auto-provisioning in the samples).
//   * Trainer user → Entra admin on SQL server. The managed identity gets a database user via
//     sql-grant-identity.sql (T-SQL – cannot be done by ARM/Bicep).
//
//   az deployment group create -g <rg> -f main.bicep \
//      -p trainerPrincipalId=$(az ad signed-in-user show --query id -o tsv) \
//         trainerPrincipalName=$(az ad signed-in-user show --query userPrincipalName -o tsv) \
//         clientIpAddress=$(curl -s https://api.ipify.org)
// ─────────────────────────────────────────────────────────────────────────────────────────────

targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Short prefix used in resource names.')
@maxLength(10)
param prefix string = 'msgtrain'

@description('Object ID of the trainer\'s Entra ID user: az ad signed-in-user show --query id -o tsv')
param trainerPrincipalId string

@description('UPN of the trainer (used as SQL Entra admin login name): az ad signed-in-user show --query userPrincipalName -o tsv')
param trainerPrincipalName string

@description('Service Bus tier. Standard covers every demo (topics, duplicate detection, transactions, scheduling). Premium adds large messages, geo-replication, private endpoints – at a fixed hourly price.')
@allowed([ 'Standard', 'Premium' ])
param serviceBusSku string = 'Standard'

@description('true = Data Sender + Data Receiver only (production-like). false = Data Owner (demo: frameworks auto-provision).')
param leastPrivilege bool = false

@description('Public IP of the trainer laptop for the SQL firewall (leave empty to skip).')
param clientIpAddress string = ''

@description('Deploy a RabbitMQ container in Azure Container Instances (otherwise use the local docker compose).')
param deployRabbitMq bool = false

@description('RabbitMQ admin password (only used when deployRabbitMq = true).')
@secure()
param rabbitMqPassword string = ''

param tags object = {
  purpose: 'training'
  workload: 'event-driven-messaging-demo'
}

var suffix = uniqueString(resourceGroup().id)
var serviceBusName = '${prefix}-sb-${suffix}'
var sqlServerName = '${prefix}-sql-${suffix}'
var identityName = '${prefix}-demo-id'

// Built-in role definition IDs
var roles = {
  serviceBusDataOwner: '090c5cfd-751d-490a-894a-3ce6f1109419'
  serviceBusDataSender: '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
  serviceBusDataReceiver: '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'
}
var serviceBusRoles = leastPrivilege ? [ roles.serviceBusDataSender, roles.serviceBusDataReceiver ] : [ roles.serviceBusDataOwner ]

// ── Managed identity for the demo apps ─────────────────────────────────────────────────────────
resource demoIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
  tags: tags
}

// ── Service Bus ────────────────────────────────────────────────────────────────────────────────
resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusName
  location: location
  tags: tags
  sku: serviceBusSku == 'Premium'
    ? { name: 'Premium', tier: 'Premium', capacity: 1 }
    : { name: 'Standard', tier: 'Standard' }
  properties: {
    disableLocalAuth: true        // Entra ID only – no SAS keys / connection strings
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled' // demo from a laptop; use private endpoints (Premium) in production
  }
}

var queueDefaults = {
  lockDuration: 'PT1M'
  maxDeliveryCount: 5
  deadLetteringOnMessageExpiration: true
  defaultMessageTimeToLive: 'P7D'
}

resource paymentsQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBus
  name: 'payments'
  properties: union(queueDefaults, {
    requiresDuplicateDetection: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
  })
}

resource rebusQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBus
  name: 'orders-rebus'
  properties: queueDefaults
}

resource eventsTopic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: serviceBus
  name: 'order-events'
  properties: {
    requiresDuplicateDetection: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    defaultMessageTimeToLive: 'P7D'
  }
}

// Each subscription = one consumer. Filters replace the implicit "$Default" (TrueFilter) rule –
// a rule with another name would be OR-ed with $Default and let everything through.
var subscriptions = [
  { name: 'billing', filterType: 'CorrelationFilter', correlationFilter: { label: 'OrderPlaced' }, sqlExpression: '' }
  { name: 'shipping', filterType: 'SqlFilter', correlationFilter: {}, sqlExpression: 'sys.Label IN (\'PaymentCharged\',\'StockReserved\')' }
  { name: 'analytics', filterType: '', correlationFilter: {}, sqlExpression: '' }
  { name: 'orders-wolverine', filterType: '', correlationFilter: {}, sqlExpression: '' }
]

resource topicSubscriptions 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = [for s in subscriptions: {
  parent: eventsTopic
  name: s.name
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 5
    deadLetteringOnMessageExpiration: true
    deadLetteringOnFilterEvaluationExceptions: true
    defaultMessageTimeToLive: 'P7D'
  }
}]

resource subscriptionRules 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2022-10-01-preview' = [for (s, i) in subscriptions: if (!empty(s.filterType)) {
  parent: topicSubscriptions[i]
  name: '$Default'
  properties: s.filterType == 'SqlFilter'
    ? { filterType: 'SqlFilter', sqlFilter: { sqlExpression: s.sqlExpression } }
    : { filterType: 'CorrelationFilter', correlationFilter: s.correlationFilter }
}]

// RBAC on the namespace for BOTH the managed identity and the trainer
resource sbRoleIdentity 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for roleId in serviceBusRoles: {
  scope: serviceBus
  name: guid(serviceBus.id, demoIdentity.id, roleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleId)
    principalId: demoIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}]

resource sbRoleTrainer 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for roleId in serviceBusRoles: {
  scope: serviceBus
  name: guid(serviceBus.id, trainerPrincipalId, roleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleId)
    principalId: trainerPrincipalId
    principalType: 'User'
  }
}]

// ── Azure SQL (Entra-only authentication) ──────────────────────────────────────────────────────
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true   // no SQL logins / passwords
      principalType: 'User'
      login: trainerPrincipalName
      sid: trainerPrincipalId
      tenantId: subscription().tenantId
    }
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'Messaging'
  location: location
  tags: tags
  sku: { name: 'Basic', tier: 'Basic' }   // ~5 DTU is plenty for the demos
}

resource sqlAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'   // lets Azure-hosted demo apps (with the managed identity) connect
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

resource sqlAllowTrainer 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = if (!empty(clientIpAddress)) {
  parent: sqlServer
  name: 'TrainerLaptop'
  properties: { startIpAddress: clientIpAddress, endIpAddress: clientIpAddress }
}

// ── Optional RabbitMQ (Azure Container Instances) ──────────────────────────────────────────────
resource rabbitMq 'Microsoft.ContainerInstance/containerGroups@2023-05-01' = if (deployRabbitMq) {
  name: '${prefix}-rabbitmq'
  location: location
  tags: tags
  properties: {
    osType: 'Linux'
    restartPolicy: 'Always'
    ipAddress: {
      type: 'Public'
      dnsNameLabel: '${prefix}-rabbit-${suffix}'
      ports: [
        { protocol: 'TCP', port: 5672 }    // AMQP (plain – demo only; use 5671/TLS for anything real)
        { protocol: 'TCP', port: 15672 }   // management UI
      ]
    }
    containers: [
      {
        name: 'rabbitmq'
        properties: {
          image: 'rabbitmq:4-management'   // consider importing into your own ACR to avoid Docker Hub rate limits
          command: [ 'bash', '-c', 'rabbitmq-plugins enable --offline rabbitmq_prometheus && rabbitmq-server' ]
          ports: [ { port: 5672 }, { port: 15672 } ]
          environmentVariables: [
            { name: 'RABBITMQ_DEFAULT_USER', value: 'trainer' }
            { name: 'RABBITMQ_DEFAULT_PASS', secureValue: rabbitMqPassword }
          ]
          resources: { requests: { cpu: 1, memoryInGB: 2 } }
        }
      }
    ]
  }
}

// ── Outputs → environment variables for the samples ────────────────────────────────────────────
output ASB_NAMESPACE string = '${serviceBus.name}.servicebus.windows.net'
output AZURE_CLIENT_ID string = demoIdentity.properties.clientId
output managedIdentityResourceId string = demoIdentity.id
output managedIdentityName string = demoIdentity.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output SQL_CONNECTION string = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDb.name};Authentication=Active Directory Default;Encrypt=True;'
output RABBITMQ_URI string = deployRabbitMq ? 'amqp://trainer:<password>@${rabbitMq.properties.ipAddress.fqdn}:5672/' : 'amqp://guest:guest@localhost:5673/ (local docker)'
output rabbitMqManagementUrl string = deployRabbitMq ? 'http://${rabbitMq.properties.ipAddress.fqdn}:15672' : ''
