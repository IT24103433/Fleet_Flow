@description('Location for all resources.')
param location string = resourceGroup().location

@description('Prefix for resource names.')
param appNamePrefix string = 'fleetflow'

@description('Administrator login name for PostgreSQL.')
param administratorLogin string = 'fleetadmin'

@description('Administrator login password for PostgreSQL.')
@secure()
param administratorLoginPassword string

@description('Shared JWT signing key for browser tokens.')
@secure()
param jwtSigningKey string = newGuid()

@description('Internal service-to-service signing key.')
@secure()
param serviceAuthSigningKey string = newGuid()

@description('Enable Azure Event Hubs with Kafka endpoint.')
param deployEventHubs bool = true

@description('SKU name for App Service Plan.')
param appServicePlanSku string = 'B1'

// 1. Azure App Service Plan (Linux)
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${appNamePrefix}-asp'
  location: location
  sku: {
    name: appServicePlanSku
    tier: 'Basic'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

// 2. PostgreSQL Flexible Server
resource postgresServer 'Microsoft.DBforPostgreSQL/flexibleServers@2023-03-01-preview' = {
  name: '${appNamePrefix}-pgserver'
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    version: '15'
    storage: {
      storageSizeGB: 32
    }
  }
}

// 3. PostgreSQL Databases for each microservice
resource authDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-03-01-preview' = {
  parent: postgresServer
  name: 'fleetflow_auth'
}

resource fleetDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-03-01-preview' = {
  parent: postgresServer
  name: 'fleetflow_fleet'
}

resource rentalDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-03-01-preview' = {
  parent: postgresServer
  name: 'fleetflow_rental'
}

resource notificationDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-03-01-preview' = {
  parent: postgresServer
  name: 'fleetflow_notifications'
}

// Allow Azure access firewall rule
resource allowAzureIPs 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-03-01-preview' = {
  parent: postgresServer
  name: 'AllowAllAzureServicesAndResourcesWithinAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// 4. Azure Key Vault for Secure Secret Storage
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: take('${appNamePrefix}-kv-${uniqueString(resourceGroup().id)}', 24)
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
  }
}

// 5. Azure Event Hubs (Kafka Enabled)
resource eventHubNamespace 'Microsoft.EventHub/namespaces@2021-11-01' = if (deployEventHubs) {
  name: '${appNamePrefix}-eventhub-${uniqueString(resourceGroup().id)}'
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
    capacity: 1
  }
  properties: {
    isAutoInflateEnabled: false
    kafkaEnabled: true
  }
}

resource bookingEventsHub 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = if (deployEventHubs) {
  parent: eventHubNamespace
  name: 'fleetflow.booking.events'
  properties: {
    messageRetentionInDays: 7
    partitionCount: 2
  }
}

resource maintenanceEventsHub 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = if (deployEventHubs) {
  parent: eventHubNamespace
  name: 'fleetflow.maintenance.events'
  properties: {
    messageRetentionInDays: 7
    partitionCount: 2
  }
}

resource vehicleEventsHub 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = if (deployEventHubs) {
  parent: eventHubNamespace
  name: 'fleetflow.vehicle.events'
  properties: {
    messageRetentionInDays: 7
    partitionCount: 2
  }
}

// Connection strings
var pgHost = postgresServer.properties.fullyQualifiedDomainName
var authConnStr = 'Server=${pgHost};Database=fleetflow_auth;Port=5432;User Id=${administratorLogin};Password=${administratorLoginPassword};Ssl Mode=Require;'
var fleetConnStr = 'Server=${pgHost};Database=fleetflow_fleet;Port=5432;User Id=${administratorLogin};Password=${administratorLoginPassword};Ssl Mode=Require;'
var rentalConnStr = 'Server=${pgHost};Database=fleetflow_rental;Port=5432;User Id=${administratorLogin};Password=${administratorLoginPassword};Ssl Mode=Require;'
var notificationConnStr = 'Server=${pgHost};Database=fleetflow_notifications;Port=5432;User Id=${administratorLogin};Password=${administratorLoginPassword};Ssl Mode=Require;'

var frontendOrigin = 'https://${appNamePrefix}-frontend.azurewebsites.net'

// 6. Identity Service Web App
resource identityApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${appNamePrefix}-identity-api'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: true
      healthCheckPath: '/health/ready'
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ConnectionStrings__DefaultConnection', value: authConnStr }
        { name: 'AUTH_DB_NAME', value: 'fleetflow_auth' }
        { name: 'Jwt__Key', value: jwtSigningKey }
        { name: 'Jwt__Issuer', value: 'FleetFlow.IdentityService' }
        { name: 'Jwt__Audience', value: 'FleetFlow.Client' }
        { name: 'SERVICE_AUTH_ENABLED', value: 'true' }
        { name: 'SERVICE_AUTH_SIGNING_KEY', value: serviceAuthSigningKey }
        { name: 'ALLOWED_ORIGINS', value: frontendOrigin }
      ]
    }
  }
}

// 7. Fleet Service Web App
resource fleetApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${appNamePrefix}-fleet-api'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: true
      healthCheckPath: '/health/ready'
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ConnectionStrings__DefaultConnection', value: fleetConnStr }
        { name: 'FLEET_DB_NAME', value: 'fleetflow_fleet' }
        { name: 'Jwt__Key', value: jwtSigningKey }
        { name: 'Jwt__Issuer', value: 'FleetFlow.IdentityService' }
        { name: 'Jwt__Audience', value: 'FleetFlow.Client' }
        { name: 'SERVICE_AUTH_ENABLED', value: 'true' }
        { name: 'SERVICE_AUTH_SIGNING_KEY', value: serviceAuthSigningKey }
        { name: 'INTERNAL_IDENTITY_URL', value: 'https://${identityApp.properties.defaultHostName}' }
        { name: 'INTERNAL_RENTAL_URL', value: 'https://${appNamePrefix}-rental-api.azurewebsites.net' }
        { name: 'INTERNAL_NOTIFICATION_URL', value: 'https://${appNamePrefix}-notification-api.azurewebsites.net' }
        { name: 'ALLOWED_ORIGINS', value: frontendOrigin }
      ]
    }
  }
}

// 8. Rental Service Web App
resource rentalApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${appNamePrefix}-rental-api'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: true
      healthCheckPath: '/health/ready'
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ConnectionStrings__DefaultConnection', value: rentalConnStr }
        { name: 'RENTAL_DB_NAME', value: 'fleetflow_rental' }
        { name: 'Jwt__Key', value: jwtSigningKey }
        { name: 'Jwt__Issuer', value: 'FleetFlow.IdentityService' }
        { name: 'Jwt__Audience', value: 'FleetFlow.Client' }
        { name: 'SERVICE_AUTH_ENABLED', value: 'true' }
        { name: 'SERVICE_AUTH_SIGNING_KEY', value: serviceAuthSigningKey }
        { name: 'INTERNAL_IDENTITY_URL', value: 'https://${identityApp.properties.defaultHostName}' }
        { name: 'INTERNAL_FLEET_URL', value: 'https://${fleetApp.properties.defaultHostName}' }
        { name: 'INTERNAL_NOTIFICATION_URL', value: 'https://${appNamePrefix}-notification-api.azurewebsites.net' }
        { name: 'ALLOWED_ORIGINS', value: frontendOrigin }
      ]
    }
  }
}

// 9. Notification Service Web App
resource notificationApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${appNamePrefix}-notification-api'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: true
      healthCheckPath: '/health/ready'
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ConnectionStrings__DefaultConnection', value: notificationConnStr }
        { name: 'NOTIFICATION_DB_NAME', value: 'fleetflow_notifications' }
        { name: 'Jwt__Key', value: jwtSigningKey }
        { name: 'Jwt__Issuer', value: 'FleetFlow.IdentityService' }
        { name: 'Jwt__Audience', value: 'FleetFlow.Client' }
        { name: 'SERVICE_AUTH_ENABLED', value: 'true' }
        { name: 'SERVICE_AUTH_SIGNING_KEY', value: serviceAuthSigningKey }
        { name: 'INTERNAL_IDENTITY_URL', value: 'https://${identityApp.properties.defaultHostName}' }
        { name: 'ALLOWED_ORIGINS', value: frontendOrigin }
      ]
    }
  }
}

// 10. Frontend Web App (React / Vite on Node 22)
resource frontendApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${appNamePrefix}-frontend'
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'NODE|22-lts'
      appCommandLine: 'node server.js'
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        { name: 'NODE_ENV', value: 'production' }
        { name: 'PORT', value: '8080' }
        { name: 'VITE_IDENTITY_API_URL', value: 'https://${identityApp.properties.defaultHostName}' }
        { name: 'VITE_FLEET_API_URL', value: 'https://${fleetApp.properties.defaultHostName}' }
        { name: 'VITE_RENTAL_API_URL', value: 'https://${rentalApp.properties.defaultHostName}' }
        { name: 'VITE_NOTIFICATION_API_URL', value: 'https://${notificationApp.properties.defaultHostName}' }
      ]
    }
  }
}

// Outputs
output identityAppName string = identityApp.name
output fleetAppName string = fleetApp.name
output rentalAppName string = rentalApp.name
output notificationAppName string = notificationApp.name
output frontendAppName string = frontendApp.name
output postgresHost string = postgresServer.properties.fullyQualifiedDomainName
output keyVaultName string = keyVault.name
output eventHubNamespaceName string = deployEventHubs ? eventHubNamespace.name : ''
