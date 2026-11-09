// Owned by the ORDERS team: the topics Orders publishes (its public contracts).
targetScope = 'resourceGroup'

param namespaceName string
param ordersPrincipalId string

var topics = [
  'orders.order-placed.v1'
  'orders.order-cancelled.v1'
  'orders.order-completed.v1'
]

module topic '../../modules/topic.bicep' = [for t in topics: {
  name: 'topic-${uniqueString(t)}'
  params: {
    namespaceName: namespaceName
    topicName: t
    senderPrincipalId: ordersPrincipalId
  }
}]
