// ─────────────────────────────────────────────────────────────────────────────
// Reusable module: ONE topic, owned by the PUBLISHING team.
// The publisher's managed identity gets "Azure Service Bus Data Sender" on this topic only.
// Topic names are part of the contract: <domain>.<event>.v<major>, e.g. orders.order-placed.v1
// ─────────────────────────────────────────────────────────────────────────────

param namespaceName string

@description('e.g. orders.order-placed.v1')
param topicName string

@description('How long an unconsumed message may live in a subscription unless the subscription says otherwise.')
param defaultMessageTimeToLive string = 'P14D'

@description('Broker-level de-duplication on MessageId (cannot be changed after creation).')
param requiresDuplicateDetection bool = true

param duplicateDetectionHistoryTimeWindow string = 'PT10M'

@description('Object id of the publisher\'s managed identity. Empty = no role assignment.')
param senderPrincipalId string = ''

var serviceBusDataSender = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39' // built-in role id

resource namespace 'Microsoft.ServiceBus/namespaces@2024-01-01' existing = {
  name: namespaceName
}

resource topic 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = {
  parent: namespace
  name: topicName
  properties: {
    defaultMessageTimeToLive: defaultMessageTimeToLive
    requiresDuplicateDetection: requiresDuplicateDetection
    duplicateDetectionHistoryTimeWindow: requiresDuplicateDetection ? duplicateDetectionHistoryTimeWindow : null
    enableBatchedOperations: true
  }
}

resource sender 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(senderPrincipalId)) {
  scope: topic
  name: guid(topic.id, senderPrincipalId, serviceBusDataSender)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataSender)
    principalId: senderPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output topicName string = topic.name
