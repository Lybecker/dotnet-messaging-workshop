// M4 demo: Application Insights (workspace-based) + a Service Bus namespace + rights for the trainer.
// Deployed by ../setup.sh. Delete everything with ../teardown.sh.
targetScope = 'resourceGroup'

param location string = resourceGroup().location

@description('Object id of the trainer: az ad signed-in-user show --query id -o tsv')
param trainerPrincipalId string

var suffix = uniqueString(resourceGroup().id)

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'logdemo-law-${suffix}'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'logdemo-appi-${suffix}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: workspace.id
  }
}

resource serviceBus 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: 'logdemo-sb-${suffix}'
  location: location
  sku: { name: 'Standard', tier: 'Standard' }     // Standard: topics for Rebus publish/subscribe
  properties: {
    disableLocalAuth: true                          // Entra ID only
    minimumTlsVersion: '1.2'
  }
}

// DEV ONLY: Rebus creates its queues, topics and subscriptions at start-up, which needs
// "Azure Service Bus Data Owner" (data-plane role with control-plane power – see the topology slides).
resource trainerIsDataOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBus.id, trainerPrincipalId, 'servicebus-data-owner')
  scope: serviceBus
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '090c5cfd-751d-490a-894a-3ce6f1109419')
    principalId: trainerPrincipalId
    principalType: 'User'
  }
}

output serviceBusNamespace string = '${serviceBus.name}.servicebus.windows.net'
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output appInsightsPortalUrl string = 'https://portal.azure.com/#resource${appInsights.id}/overview'
