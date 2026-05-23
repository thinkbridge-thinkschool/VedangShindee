targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment used for naming Azure resources')
param environmentName string

@minLength(1)
@description('Primary Azure region for all resources')
param location string

@description('Container image for the API service — populated by azd after push')
param apiImageName string = ''

var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = { 'azd-env-name': environmentName }

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  name: 'resources'
  scope: rg
  params: {
    location: location
    tags: tags
    resourceToken: resourceToken
    apiImageName: apiImageName
  }
}

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = resources.outputs.containerRegistryLoginServer
output SERVICE_API_URL string = resources.outputs.serviceApiUrl
output SERVICE_API_NAME string = resources.outputs.serviceApiName
