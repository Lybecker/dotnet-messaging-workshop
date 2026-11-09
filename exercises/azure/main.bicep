// Exercise-only alternative to Docker. Each attendee gets a dedicated namespace.
targetScope = 'resourceGroup'

param location string = resourceGroup().location

@description('Object ID of the attendee signing in with Azure CLI. Not an application/client ID.')
param attendeePrincipalId string

@description('Globally unique namespace name. Use a separate resource group per attendee for the default.')
@minLength(6)
@maxLength(50)
param namespaceName string = 'msg-ex-${uniqueString(resourceGroup().id)}'

resource serviceBus 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: namespaceName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    disableLocalAuth: true
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Training only: frameworks create queues/topics/subscriptions as attendees complete TODOs.
resource attendeeDataOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: serviceBus
  name: guid(serviceBus.id, attendeePrincipalId, 'exercise-data-owner')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '090c5cfd-751d-490a-894a-3ce6f1109419')
    principalId: attendeePrincipalId
    principalType: 'User'
  }
}

output serviceBusNamespace string = '${serviceBus.name}.servicebus.windows.net'
