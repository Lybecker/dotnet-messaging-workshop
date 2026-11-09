// ─────────────────────────────────────────────────────────────────────────────
// Reusable module: ONE topic subscription, owned by the CONSUMING team.
//
//  * The topic belongs to the publisher (see topic.bicep) and must already exist.
//  * Sensible, explicit defaults: nothing is left to a framework or the portal.
//  * Optional SQL filter (replaces the $Default "1=1" rule, otherwise the filter has no effect).
//  * Optional forwardTo: Rebus-style endpoints receive from their own input queue, so the
//    subscription forwards there. Wolverine/native SDK can receive from the subscription directly.
//  * Data-plane only for the app: the consumer's managed identity gets "Azure Service Bus Data Receiver"
//    on exactly this subscription (or on the forward-to queue, see queue.bicep).
//
// Publish to a private registry so every team uses the same version:
//   az bicep publish --file subscription.bicep --target br:<acr>.azurecr.io/bicep/servicebus/subscription:1.0.0
// ─────────────────────────────────────────────────────────────────────────────

@description('Name of the existing Service Bus namespace (one shared namespace per environment).')
param namespaceName string

@description('Existing topic, owned by the publisher, e.g. orders.order-placed.v1')
param topicName string

@description('Subscription name = the consuming service, e.g. billing')
@maxLength(50)
param subscriptionName string

@description('Optional SQL filter, e.g. "country = \'DK\'". Empty = receive everything.')
param sqlFilter string = ''

@description('Optional queue to forward to (e.g. the Rebus input queue "billing"). Empty = receive from the subscription.')
param forwardTo string = ''

@description('Deliveries before dead-lettering. Must be HIGHER than the retries your framework does itself.')
@minValue(1)
param maxDeliveryCount int = 10

@description('Peek-lock duration (ISO 8601). Frameworks renew the lock for long handlers.')
param lockDuration string = 'PT1M'

@description('How long an unprocessed message may wait (ISO 8601).')
param defaultMessageTimeToLive string = 'P14D'

@description('Set true for ordered processing per key (sessions).')
param requiresSession bool = false

@description('Object id of the consumer\'s managed identity. Empty = no role assignment.')
param receiverPrincipalId string = ''

var serviceBusDataReceiver = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0' // built-in role id

resource namespace 'Microsoft.ServiceBus/namespaces@2024-01-01' existing = {
  name: namespaceName
}

resource topic 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' existing = {
  parent: namespace
  name: topicName
}

resource subscription 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2024-01-01' = {
  parent: topic
  name: subscriptionName
  properties: {
    maxDeliveryCount: maxDeliveryCount
    lockDuration: lockDuration
    defaultMessageTimeToLive: defaultMessageTimeToLive
    deadLetteringOnMessageExpiration: true          // expired messages are kept for inspection, not dropped
    deadLetteringOnFilterEvaluationExceptions: true
    requiresSession: requiresSession
    forwardTo: empty(forwardTo) ? null : forwardTo
  }
}

// Overwrite the $Default rule; adding a second rule next to "1=1" would let everything through.
resource filter 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2024-01-01' = if (!empty(sqlFilter)) {
  parent: subscription
  name: '$Default'
  properties: {
    filterType: 'SqlFilter'
    sqlFilter: {
      sqlExpression: sqlFilter
    }
  }
}

// Data plane for the app: receive from this subscription only.
// (With forwardTo, grant the receiver role on the target queue instead – see queue.bicep.)
resource receiver 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(receiverPrincipalId) && empty(forwardTo)) {
  scope: subscription
  name: guid(subscription.id, receiverPrincipalId, serviceBusDataReceiver)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataReceiver)
    principalId: receiverPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output subscriptionId string = subscription.id
