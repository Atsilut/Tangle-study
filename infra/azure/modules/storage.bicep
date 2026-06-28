@description('Blob storage for media uploads.')
param name string
param location string
param containerName string = 'tangle-media'
@description('Browser origins allowed for blob PUT (SAS uploads). Use ["*"] for dev only.')
param allowedBlobOrigins array = []
param tags object = {}

var corsOrigins = length(allowedBlobOrigins) > 0 ? allowedBlobOrigins : ['*']

resource account 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: account
  name: 'default'
  properties: {
    cors: {
      corsRules: [
        {
          allowedOrigins: corsOrigins
          allowedMethods: ['GET', 'PUT', 'OPTIONS', 'HEAD']
          allowedHeaders: ['*']
          exposedHeaders: ['*']
          maxAgeInSeconds: 3600
        }
      ]
    }
  }
}

resource mediaContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: containerName
  properties: {
    publicAccess: 'None'
  }
}

output accountName string = account.name
output blobEndpoint string = account.properties.primaryEndpoints.blob
output containerName string = containerName
output accountKey string = account.listKeys().keys[0].value
