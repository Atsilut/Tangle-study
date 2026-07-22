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
param env object = {}
@description('Secret refs: { name, envName }. Values come from secretValues by name.')
param secretEnvVars array = []
@description('Map of secret name → value (deduped).')
@secure()
param secretValues object = {}
param registryLoginServer string = ''
param registryUsername string = ''
@secure()
param registryPassword string = ''
@description('HTTP path for liveness/readiness. Leave empty to skip probes (placeholder images).')
param healthCheckPath string = ''
@description('Extra env vars merged after env (e.g. computed blob endpoint).')
param extraEnvVars array = []
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

var envVars = [for item in items(env): {
  name: item.key
  value: string(item.value)
}]

var allSecretNames = [for item in secretEnvVars: item.name]
var uniqueSecretNames = empty(allSecretNames) ? [] : union(allSecretNames, allSecretNames)

// Empty string is a valid Bicep default for @secure() params; treat it like missing
// so local bootstrap without secrets can still create apps (CD overwrites later).
var appSecretDefinitions = [for secretName in uniqueSecretNames: {
  name: secretName
  value: empty(secretValues[?secretName] ?? '') ? 'pending-deploy' : secretValues[secretName]
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
          env: concat(envVars, extraEnvVars, secretEnvMappings)
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
