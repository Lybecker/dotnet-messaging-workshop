// Service Bus topology as code. Pattern for DECENTRALISED ownership on a SHARED namespace:
//   * Platform team owns the namespace (tier, networking, RBAC, diagnostics, alerts)   → namespace.bicep
//   * Publishing team owns its topic                                                    → this module, "topic" part
//   * Each consuming team owns its subscription + filter, deployed from ITS OWN repo    → "subscription" part
// Apps get data-plane roles only (Azure Service Bus Data Sender/Receiver) – never Manage / connection strings.

param namespaceName string
param location string = resourceGroup().location
param senderPrincipalId string          // managed identity of orders-api
param billingPrincipalId string         // managed identity of billing

resource ns 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: namespaceName
  location: location
  sku: { name: 'Premium', tier: 'Premium', capacity: 1 }
  properties: {
    disableLocalAuth: true              // Entra ID only, no SAS keys
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Disabled'     // private endpoints
  }
}

resource topic 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = {
  parent: ns
  name: 'order-events'
  properties: {
    requiresDuplicateDetection: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    defaultMessageTimeToLive: 'P7D'
  }
}

resource billingSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = {
  parent: topic
  name: 'billing'
  properties: {
    maxDeliveryCount: 5
    lockDuration: 'PT1M'
    deadLetteringOnMessageExpiration: true
    deadLetteringOnFilterEvaluationExceptions: true
  }
}

resource billingRule 'Microsoft.ServiceBus/namespaces/topics/subscriptions/rules@2022-10-01-preview' = {
  parent: billingSub
  name: 'only-order-placed'
  properties: {
    filterType: 'CorrelationFilter'
    correlationFilter: { label: 'OrderPlaced' }
  }
}

// Built-in role definition IDs
var dataSender   = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'   // Azure Service Bus Data Sender
var dataReceiver = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'   // Azure Service Bus Data Receiver

resource sendOnTopic 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: topic
  name: guid(topic.id, senderPrincipalId, dataSender)
  properties: { roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', dataSender), principalId: senderPrincipalId, principalType: 'ServicePrincipal' }
}

resource receiveOnSub 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: billingSub
  name: guid(billingSub.id, billingPrincipalId, dataReceiver)
  properties: { roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', dataReceiver), principalId: billingPrincipalId, principalType: 'ServicePrincipal' }
}
