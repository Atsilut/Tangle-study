@description('Generic Container App with optional ingress and optional GHCR/private registry auth.')
param name string
param location string
param managedEnvironmentId string
param containerImage string
param targetPort int = 8080
param enableIngress bool = true
param externalIngress bool = false
param ingressTransport string = 'auto'
param minReplicas int = 1
param maxReplicas int = 3
param cpu string = '0.5'
param memory string = '1Gi'
param envVars array = []
@description('Secret refs: { name, envName, value? }. Omit value for CD-injected secrets.')
param secretEnvVars array = []
param registryLoginServer string = ''
param registryUsername string = ''
@secure()
param registryPassword string = ''
@description('HTTP path for liveness/readiness. Leave empty to skip probes (placeholder images).')
param healthCheckPath string = ''
param tags object = {}

var hasRegistry = !empty(registryLoginServer) && !empty(registryUsername) && !empty(registryPassword)
var probeScheme = externalIngress ? 'HTTPS' : 'HTTP'
var probes = empty(healthCheckPath) ? [] : [
  {
    type: 'Liveness'
    httpGet: {
      path: healthCheckPath
      port: targetPort
      scheme: probeScheme
    }
    initialDelaySeconds: 15
    periodSeconds: 10
  }
  {
    type: 'Readiness'
    httpGet: {
      path: healthCheckPath
      port: targetPort
      scheme: probeScheme
    }
    initialDelaySeconds: 5
    periodSeconds: 5
  }
]

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

resource app 'Microsoft.App/containerApps@2026-01-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: managedEnvironmentId
    configuration: union(
      {
        activeRevisionsMode: 'Single'
        registries: hasRegistry ? [
          {
            server: registryLoginServer
            username: registryUsername
            passwordSecretRef: 'registry-password'
          }
        ] : []
        secrets: concat(registrySecrets, appSecretDefinitions)
      },
      enableIngress ? {
        ingress: {
          external: externalIngress
          targetPort: targetPort
          transport: ingressTransport
          allowInsecure: false
        }
      } : {}
    )
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
          probes: probes
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

output id string = app.id
output name string = app.name
output fqdn string = enableIngress ? app.properties.configuration.ingress.fqdn : ''
