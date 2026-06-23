@description('Container Apps Job for one-shot EF migrations.')
param name string
param location string
param managedEnvironmentId string
param containerImage string
param cpu string = '0.5'
param memory string = '1Gi'
param envVars array = []
param secretEnvVars array = []
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

var appSecretDefinitions = [for item in secretEnvVars: {
  name: item.name
  value: item.?value ?? 'pending-deploy'
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
          command: [
            'dotnet'
            'Api.dll'
            '--migrate'
          ]
        }
      ]
    }
  }
}

output id string = job.id
output name string = job.name
