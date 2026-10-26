targetScope = 'resourceGroup'

@description('Short app name, used to derive resource names.')
param appName string = 'notesstarter'

param location string = resourceGroup().location

@description('dev | staging | prod')
param environment string = 'dev'

@description('App Service plan SKU. Deployment slots need S1 or higher.')
param planSku string = 'B1'

@description('Allowed browser origin for the API (the UI URL). Leave empty to allow none.')
param uiOrigin string = ''

var suffix = toLower('${appName}-${environment}-${uniqueString(resourceGroup().id)}')
var slotsEnabled = !contains(['B1', 'B2', 'B3', 'F1', 'D1'], planSku)

// Observability: one workspace per environment, App Insights on top of it
resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${suffix}'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource insights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${suffix}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
  }
}

// Identity the API uses to reach the database (no secrets)
resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${suffix}'
  location: location
}

// NoSQL storage: Cosmos DB serverless, pay only for what you use
resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: 'cosmos-${take(suffix, 36)}'
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    capabilities: [{ name: 'EnableServerless' }]
    locations: [{ locationName: location, failoverPriority: 0 }]
    consistencyPolicy: { defaultConsistencyLevel: 'Session' }
    disableLocalAuth: true
  }
}

resource db 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmos
  name: 'notesdb'
  properties: { resource: { id: 'notesdb' } }
}

resource container 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: db
  name: 'notes'
  properties: {
    resource: {
      id: 'notes'
      partitionKey: { paths: ['/userId'], kind: 'Hash' }
    }
  }
}

// Built-in "Cosmos DB Built-in Data Contributor" role
resource dataRole 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = {
  parent: cosmos
  name: guid(cosmos.id, identity.id, 'data-contributor')
  properties: {
    roleDefinitionId: '${cosmos.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    principalId: identity.properties.principalId
    scope: cosmos.id
  }
}

// Hosting: one plan, one web app per service
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-${suffix}'
  location: location
  kind: 'linux'
  sku: { name: planSku }
  properties: { reserved: true }
}

resource api 'Microsoft.Web/sites@2023-12-01' = {
  name: 'app-api-${suffix}'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      alwaysOn: planSku != 'F1'
      healthCheckPath: '/health'
      appSettings: [
        { name: 'AZURE_CLIENT_ID', value: identity.properties.clientId }
        { name: 'Cosmos__Endpoint', value: cosmos.properties.documentEndpoint }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: insights.properties.ConnectionString }
        { name: 'Cors__AllowedOrigins__0', value: uiOrigin }
      ]
    }
  }
}

resource apiSlot 'Microsoft.Web/sites/slots@2023-12-01' = if (slotsEnabled) {
  parent: api
  name: 'staging'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: { linuxFxVersion: 'DOTNETCORE|10.0', minTlsVersion: '1.2', ftpsState: 'Disabled' }
  }
}

// UI is static files, so Static Web Apps is the cheapest host
resource ui 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'stapp-${suffix}'
  location: location
  sku: { name: 'Free', tier: 'Free' }
  properties: {}
}

// Central logging: send API logs and metrics to the workspace
resource apiDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: api
  properties: {
    workspaceId: logs.id
    logs: [
      { category: 'AppServiceHTTPLogs', enabled: true }
      { category: 'AppServiceConsoleLogs', enabled: true }
    ]
    metrics: [{ category: 'AllMetrics', enabled: true }]
  }
}

// Optional custom domain: creates a DNS zone you point your registrar's name servers at
@description('Custom domain, e.g. notes.example.com. Leave empty to skip DNS.')
param customDomain string = ''

resource dns 'Microsoft.Network/dnsZones@2018-05-01' = if (!empty(customDomain)) {
  name: customDomain
  location: 'global'
}

resource apiRecord 'Microsoft.Network/dnsZones/CNAME@2018-05-01' = if (!empty(customDomain)) {
  parent: dns
  name: 'api'
  properties: {
    TTL: 3600
    CNAMERecord: { cname: api.properties.defaultHostName }
  }
}

resource wwwRecord 'Microsoft.Network/dnsZones/CNAME@2018-05-01' = if (!empty(customDomain)) {
  parent: dns
  name: 'www'
  properties: {
    TTL: 3600
    CNAMERecord: { cname: ui.properties.defaultHostname }
  }
}

output apiUrl string = 'https://${api.properties.defaultHostName}'
output uiUrl string = 'https://${ui.properties.defaultHostname}'
output cosmosEndpoint string = cosmos.properties.documentEndpoint
