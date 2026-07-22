@description('Container Apps Job for one-shot EF migrations.')
param name string
param location string
param managedEnvironmentId string
param containerImage string
param cpu string = '0.5'
param memory string = '1Gi'
param env object = {}
param secretEnvVars array = []
@secure()
param secretValues object = {}
@description('Container command, e.g. ["dotnet", "Users.dll", "--migrate"].')
param command array = ['dotnet', 'Users.dll', '--migrate']
param registryLoginServer string = ''
param registryUsername string = ''
@secure()
param registryPassword string = ''
param tags object = {}

var hasRegistry = !empty(registryLoginServer) && !empty(registryUsername) && !empty(registryPassword)

var registrySecrets = hasRegistry ? [
  {
    name: 'registry-password'
    value: registryPassword
  }
] : []

var envVars = [for item in items(env): {
  name: item.key
  value: string(item.value)
}]

var allSecretNames = [for item in secretEnvVars: item.name]
var uniqueSecretNames = empty(allSecretNames) ? [] : union(allSecretNames, allSecretNames)

// Empty string is a valid Bicep default for @secure() params; treat it like missing
// so local bootstrap without secrets can still create jobs (CD overwrites later).
var appSecretDefinitions = [for secretName in uniqueSecretNames: {
  name: secretName
  value: empty(secretValues[?secretName] ?? '') ? 'pending-deploy' : secretValues[secretName]
}]

var secretEnvMappings = [for item in secretEnvVars: {
  name: item.envName
  secretRef: item.name
}]

resource job 'Microsoft.App/jobs@2024-03-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    environmentId: managedEnvironmentId
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      replicaRetryLimit: 1
      registries: hasRegistry ? [
        {
          server: registryLoginServer
          username: registryUsername
          passwordSecretRef: 'registry-password'
        }
      ] : []
      secrets: concat(registrySecrets, appSecretDefinitions)
    }
    template: {
      containers: [
        {
          name: name
          image: containerImage
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          env: concat(envVars, secretEnvMappings)
          command: command
        }
      ]
    }
  }
}

output id string = job.id
output name string = job.name
