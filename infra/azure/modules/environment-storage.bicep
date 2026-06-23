@description('Bind Azure File storage to a Container Apps environment (e.g. Postgres data).')
param managedEnvironmentName string
param storageAccountName string
@secure()
param storageAccountKey string
param storageBindingName string = 'postgres-data'
param fileShareName string = 'postgres-data'

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: managedEnvironmentName
}

resource envStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: environment
  name: storageBindingName
  properties: {
    azureFile: {
      accountName: storageAccountName
      accountKey: storageAccountKey
      shareName: fileShareName
      accessMode: 'ReadWrite'
    }
  }
}

output storageBindingName string = envStorage.name
