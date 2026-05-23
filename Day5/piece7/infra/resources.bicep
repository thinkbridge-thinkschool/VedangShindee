param location string
param tags object
param resourceToken string

@description('Container image for the API — empty on first deploy, set by azd on subsequent runs')
param apiImageName string = ''

// Reuse the shared Container Apps Environment (subscription limit = 1)
resource existingEnv 'Microsoft.App/managedEnvironments@2023-05-01' existing = {
  name: 'cae-342m3golxdrt6'
  scope: resourceGroup('rg-quotes-amey')
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: 'cr${resourceToken}'
  location: location
  tags: tags
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: true
  }
}

resource api 'Microsoft.App/containerApps@2023-05-01' = {
  name: 'ca-api-${resourceToken}'
  location: location
  tags: union(tags, { 'azd-service-name': 'api' })
  properties: {
    managedEnvironmentId: existingEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          username: containerRegistry.name
          passwordSecretRef: 'registry-password'
        }
      ]
      secrets: [
        {
          name: 'registry-password'
          value: containerRegistry.listCredentials().passwords[0].value
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: !empty(apiImageName) ? apiImageName : 'mcr.microsoft.com/dotnet/samples:aspnetapp'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'ConnectionStrings__Default', value: 'Data Source=/tmp/quotes.db' }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

output containerRegistryLoginServer string = containerRegistry.properties.loginServer
output serviceApiUrl string = 'https://${api.properties.configuration.ingress.fqdn}'
output serviceApiName string = api.name
