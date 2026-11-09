// Owned by the PLATFORM team: the one shared namespace for this environment.
// Deployed as a stack with --action-on-unmanage detachAll: removing it from this file never deletes the bus.
// No CanNotDelete lock here: locks are inherited by child resources and would stop the team stacks
// from deleting the subscriptions they remove.
param name string
param location string = resourceGroup().location
@allowed([ 'Standard', 'Premium' ])
param sku string = 'Standard'

resource ns 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: name
  location: location
  sku: { name: sku, tier: sku }
  properties: {
    disableLocalAuth: true        // Entra ID only: no SAS keys or connection strings
    minimumTlsVersion: '1.2'
  }
}

output namespaceName string = ns.name
