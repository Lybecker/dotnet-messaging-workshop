// Owned by the BILLING team: everything Billing listens to. Add a line to subscribe,
// delete a line to unsubscribe (deployed as a deployment stack, so removed entries are deleted).
targetScope = 'resourceGroup'

param namespaceName string
param billingPrincipalId string

var subscriptions = [
  { topic: 'orders.order-placed.v1',     filter: '' }
  { topic: 'orders.order-cancelled.v1',  filter: '' }
  { topic: 'payments.payment-failed.v1', filter: 'IsPermanent = true' }
]

module sub '../modules/subscription.bicep' = [for s in subscriptions: {
  name: 'sub-billing-${uniqueString(s.topic)}'
  params: {
    namespaceName: namespaceName
    topicName: s.topic
    subscriptionName: 'billing'
    sqlFilter: s.filter
    receiverPrincipalId: billingPrincipalId
    // Rebus endpoint? forward everything to its input queue instead:
    // forwardTo: 'billing'
  }
}]
