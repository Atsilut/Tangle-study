targetScope = 'resourceGroup'

@description('Short project name used in Azure resource names (lowercase alphanumeric).')
param baseName string = 'tanglestudy'

@description('Environment slug: dev, staging, or prod.')
param environment string = 'dev'

param location string = resourceGroup().location

@description('GHCR image prefix, e.g. ghcr.io/your-org/tangle-study. Public packages need no registry auth.')
param containerRegistry string = 'ghcr.io/your-org/tangle-study'

@description('Container image tag (git SHA or latest).')
param imageTag string = 'latest'

@description('Use public placeholder images until the CD pipeline pushes to GHCR.')
param usePlaceholderImages bool = true

@description('GHCR username for private packages. Leave empty when images are public.')
param registryUsername string = ''

@secure()
param registryPassword string = ''

@description('Pinned infra image tags (postgres, redis, exporters, upstream prometheus/grafana).')
param infra object = {}

@description('Pinned build-base image tags (dotnet, node, nginx, rust, debian). Kept for parameters.prod.json single source of truth; unused by Bicep runtime.')
#disable-next-line no-unused-params
param buildImages object = {}

@description('Container Apps map from parameters.prod.json (single source of truth).')
param containerApps object = {}

@description('EF migrate jobs from parameters.prod.json.')
param migrateJobs array = []

@secure()
param postgresConnectionString string = ''

@secure()
param postgresExporterDsn string = ''

@secure()
param blobConnectionString string = ''

@secure()
param jwtSecret string = ''

@secure()
param workerCallbackSecret string = ''

@secure()
param metricsScrapeSecret string = ''

@secure()
param placesApiKey string = ''

@secure()
param applicationInsightsConnectionString string = ''

@secure()
param grafanaAdminPassword string = ''

@secure()
param gatewaySecret string = ''

@secure()
param usersInternalSecret string = ''

@secure()
param mediaInternalSecret string = ''

@secure()
param chatInternalSecret string = ''

@secure()
param locationInternalSecret string = ''

@secure()
param communityInternalSecret string = ''

@secure()
param groupInternalSecret string = ''

@secure()
param socialInternalSecret string = ''

param tags object = {
  project: 'tangle-study'
  environment: environment
}

var namePrefix = '${baseName}${environment}'
var placeholderImage = 'mcr.microsoft.com/k8se/quickstart:latest'

// Secret name → value map for containerApps.secretEnvVars and migrate jobs.
var secretValues = {
  'postgres-conn': postgresConnectionString
  'postgres-dsn': postgresExporterDsn
  'blob-conn': blobConnectionString
  'jwt-secret': jwtSecret
  'worker-callback': workerCallbackSecret
  'metrics-secret': metricsScrapeSecret
  'places-api-key': placesApiKey
  'appinsights-conn': applicationInsightsConnectionString
  'grafana-admin-password': grafanaAdminPassword
  'gateway-secret': gatewaySecret
  'users-internal-secret': usersInternalSecret
  'media-internal-secret': mediaInternalSecret
  'chat-internal-secret': chatInternalSecret
  'location-internal-secret': locationInternalSecret
  'community-internal-secret': communityInternalSecret
  'group-internal-secret': groupInternalSecret
  'social-internal-secret': socialInternalSecret
}

var customImageTypes = [
  'gateway'
  'users'
  'media'
  'chat'
  'location'
  'community'
  'group'
  'social'
  'web'
  'worker'
]

var appEntries = items(containerApps)

module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'log-analytics'
  params: {
    name: '${namePrefix}-logs'
    location: location
    tags: tags
  }
}

module appInsights 'modules/app-insights.bicep' = {
  name: 'app-insights'
  params: {
    name: '${namePrefix}-appi'
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.id
    tags: tags
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    name: take('${namePrefix}sa', 24)
    location: location
    tags: tags
    allowedBlobOrigins: ['*']
  }
}

module containerAppsEnv 'modules/container-apps-env.bicep' = {
  name: 'container-apps-env'
  params: {
    name: '${namePrefix}-cae'
    location: location
    logAnalyticsCustomerId: logAnalytics.outputs.customerId
    logAnalyticsSharedKey: logAnalytics.outputs.sharedKey
    tags: tags
  }
}

// Redis is infrastructure (TCP ingress); always provisioned when present in containerApps.
module redis 'modules/infra-container.bicep' = if (contains(containerApps, 'tangle-study-redis')) {
  name: 'infra-redis'
  params: {
    name: 'tangle-study-redis'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: infra.?redis.?image ?? 'redis:8-alpine'
    minReplicas: containerApps['tangle-study-redis'].?minReplicas ?? 1
    maxReplicas: containerApps['tangle-study-redis'].?maxReplicas ?? 1
    tcpProbePort: 6379
    tags: tags
    envVars: []
    secretEnvVars: []
  }
}

module apps 'modules/container-app.bicep' = [
  for item in appEntries: if (item.value.type != 'redis') {
    name: 'app-${item.key}'
    params: {
      name: item.key
      location: location
      managedEnvironmentId: containerAppsEnv.outputs.id
      containerImage: !empty(item.value.?image)
        ? (usePlaceholderImages && contains(customImageTypes, item.value.type)
            ? placeholderImage
            : (usePlaceholderImages && item.value.type == 'prometheus'
                ? infra.prometheus.image
                : (usePlaceholderImages && item.value.type == 'grafana'
                    ? infra.grafana.image
                    : '${containerRegistry}/${item.value.image}:${imageTag}')))
        : (!empty(item.value.?infraImage)
            ? infra[item.value.infraImage].image
            : placeholderImage)
      targetPort: usePlaceholderImages && contains(customImageTypes, item.value.type)
        ? 80
        : (item.value.?targetPort ?? 8080)
      enableIngress: true
      externalIngress: item.value.?externalIngress ?? false
      ingressTransport: item.value.?ingressTransport ?? 'auto'
      minReplicas: item.value.?minReplicas ?? 1
      maxReplicas: item.value.?maxReplicas ?? 3
      healthCheckPath: usePlaceholderImages && contains(customImageTypes, item.value.type)
        ? ''
        : (item.value.?healthCheckPath ?? '')
      registryLoginServer: !empty(item.value.?image) ? 'ghcr.io' : ''
      registryUsername: registryUsername
      registryPassword: registryPassword
      tags: tags
      env: item.value.?env ?? {}
      secretEnvVars: item.value.?secretEnvVars ?? []
      secretValues: secretValues
      extraEnvVars: item.value.type == 'media' ? [
        { name: 'Media__PublicBlobEndpoint', value: storage.outputs.blobEndpoint }
        { name: 'Media__ContainerName', value: storage.outputs.containerName }
      ] : []
    }
  }
]

module migrate 'modules/migrate-job.bicep' = [
  for job in migrateJobs: {
    name: 'migrate-${job.name}'
    params: {
      name: job.name
      location: location
      managedEnvironmentId: containerAppsEnv.outputs.id
      containerImage: usePlaceholderImages
        ? placeholderImage
        : '${containerRegistry}/${job.image}:${imageTag}'
      registryLoginServer: 'ghcr.io'
      registryUsername: registryUsername
      registryPassword: registryPassword
      tags: tags
      command: job.command
      env: {
        ASPNETCORE_ENVIRONMENT: 'Production'
      }
      secretEnvVars: [
        {
          name: 'postgres-conn'
          envName: 'ConnectionStrings__DefaultConnection'
        }
      ]
      secretValues: secretValues
    }
  }
]

output prometheusInternalUrl string = 'http://tangle-study-prometheus'
output redisAppName string = contains(containerApps, 'tangle-study-redis') ? 'tangle-study-redis' : ''
output blobEndpoint string = storage.outputs.blobEndpoint
output appInsightsConnectionString string = appInsights.outputs.connectionString
output containerAppsEnvironmentId string = containerAppsEnv.outputs.id
output migrateJobNames array = [for job in migrateJobs: job.name]
output containerAppNames array = [for item in appEntries: item.key]
