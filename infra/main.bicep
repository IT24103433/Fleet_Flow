@description('Location for all resources.')
param location string = 'koreacentral'

@description('Prefix for resource names.')
param appNamePrefix string = 'fleetflow'

@description('Administrator login name for PostgreSQL.')
param administratorLogin string = 'fleetadmin'

@description('Administrator login password for PostgreSQL.')
@secure()
param administratorLoginPassword string

// 1. Azure App Service Plan (Linux)
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${appNamePrefix}-asp'
  location: location
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

// 2. Identity Service Web App
resource identityApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${appNamePrefix}-identity-api'
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      appSettings: [
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: 'Server=${postgresServer.properties.fullyQualifiedDomainName};Database=fleetflow_auth;Port=5432;User Id=${administratorLogin};Password=${administratorLoginPassword};Ssl Mode=Require;'
        }
      ]
    }
  }
}

// 3. Fleet Service Web App
resource fleetApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${appNamePrefix}-fleet-api'
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      appSettings: [
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: 'Server=${postgresServer.properties.fullyQualifiedDomainName};Database=fleetflow_fleet;Port=5432;User Id=${administratorLogin};Password=${administratorLoginPassword};Ssl Mode=Require;'
        }
      ]
    }
  }
}

// 4. PostgreSQL Flexible Server
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

// Databases
resource authDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-03-01-preview' = {
  parent: postgresServer
  name: 'fleetflow_auth'
}

resource fleetDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-03-01-preview' = {
  parent: postgresServer
  name: 'fleetflow_fleet'
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

// 5. Frontend Web App (React SPA on Linux App Service)
resource frontendApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${appNamePrefix}-frontend'
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'NODE|20-lts'
      appCommandLine: 'npx serve -s .'
    }
  }
}

output identityAppName string = identityApp.name
output fleetAppName string = fleetApp.name
output frontendAppName string = frontendApp.name
output postgresHost string = postgresServer.properties.fullyQualifiedDomainName
