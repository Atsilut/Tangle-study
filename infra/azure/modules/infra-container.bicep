@description('Internal infrastructure container (Redis) with optional internal TCP ingress when tcpProbePort is set.')
param name string
param location string
param managedEnvironmentId string
param containerImage string
param envVars array = []
param secretEnvVars array = []
param minReplicas int = 1
param maxReplicas int = 1
param cpu string = '0.5'
param memory string = '1Gi'
@description('TCP port for liveness/readiness probes. 0 disables probes.')
param tcpProbePort int = 0
@description('Container Apps environment storage binding for Azure File volume.')
param environmentStorageName string = ''
param volumeMountPath string = ''
param tags object = {}

var probes = tcpProbePort > 0 ? [
  {
    type: 'Liveness'
    tcpSocket: {
      port: tcpProbePort
    }
    initialDelaySeconds: 20
    periodSeconds: 10
  }
  {
    type: 'Readiness'
    tcpSocket: {
      port: tcpProbePort
    }
    initialDelaySeconds: 5
    periodSeconds: 5
  }
] : []

var hasVolume = !empty(environmentStorageName) && !empty(volumeMountPath)

var secretDefinitions = [for item in secretEnvVars: {
  name: item.name
  value: item.?value ?? 'pending-deploy'
}]

var secretEnvMappings = [for item in secretEnvVars: {
  name: item.envName
  secretRef: item.name
}]

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: managedEnvironmentId
    configuration: union(
      {
        activeRevisionsMode: 'Single'
        secrets: secretDefinitions
      },
      tcpProbePort > 0 ? {
        ingress: {
          external: false
          targetPort: tcpProbePort
          exposedPort: tcpProbePort
          transport: 'tcp'
        }
      } : {}
    )
    template: {
      volumes: hasVolume ? [
        {
          name: 'data'
          storageType: 'AzureFile'
          storageName: environmentStorageName
        }
      ] : []
      containers: [
        {
          name: name
          image: containerImage
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          env: concat(envVars, secretEnvMappings)
          volumeMounts: hasVolume ? [
            {
              volumeName: 'data'
              mountPath: volumeMountPath
            }
          ] : []
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

output name string = app.name
