// ─────────────────────────────────────────────────────────────────────────────
// Reusable module: ONE queue, owned by the RECEIVING team (command queue or a Rebus input queue).
// Grants Data Receiver to the owner and, optionally, Data Sender to the services allowed to send to it.
// ─────────────────────────────────────────────────────────────────────────────

param namespaceName string

@description('e.g. payments (commands) or billing (Rebus input queue)')
param queueName string

@description('Deliveries before dead-lettering. Must be HIGHER than the retries your framework does itself.')
param maxDeliveryCount int = 10
param lockDuration string = 'PT1M'
param defaultMessageTimeToLive string = 'P14D'
param requiresSession bool = false
param requiresDuplicateDetection bool = false

@description('Object id of the identity that receives from this queue.')
param receiverPrincipalId string = ''

@description('Object ids of identities allowed to send commands to this queue.')
param senderPrincipalIds array = []

var serviceBusDataReceiver = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'
var serviceBusDataSender = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'

resource namespace 'Microsoft.ServiceBus/namespaces@2024-01-01' existing = {
  name: namespaceName
}

resource queue 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = {
  parent: namespace
  name: queueName
  properties: {
    maxDeliveryCount: maxDeliveryCount
    lockDuration: lockDuration
    defaultMessageTimeToLive: defaultMessageTimeToLive
    deadLetteringOnMessageExpiration: true
    requiresSession: requiresSession
    requiresDuplicateDetection: requiresDuplicateDetection
  }
}

resource receiver 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(receiverPrincipalId)) {
  scope: queue
  name: guid(queue.id, receiverPrincipalId, serviceBusDataReceiver)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataReceiver)
    principalId: receiverPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource senders 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for p in senderPrincipalIds: {
  scope: queue
  name: guid(queue.id, p, serviceBusDataSender)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', serviceBusDataSender)
    principalId: p
    principalType: 'ServicePrincipal'
  }
}]
