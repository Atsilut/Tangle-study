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

@secure()
param postgresAdminPassword string

param postgresAdminLogin string = 'tangle'

param postgresImage string = 'postgres:18'

param redisImage string = 'redis:8-alpine'

param apiMinReplicas int = 1
param webMinReplicas int = 1
param workerMinReplicas int = 0

param tags object = {
  project: 'tangle-study'
  environment: environment
}

var namePrefix = '${baseName}${environment}'
var placeholderImage = 'mcr.microsoft.com/k8se/quickstart:latest'
var apiImage = usePlaceholderImages ? placeholderImage : '${containerRegistry}/tangle-api:${imageTag}'
var webImage = usePlaceholderImages ? placeholderImage : '${containerRegistry}/tangle-web:${imageTag}'
var workerImage = usePlaceholderImages ? placeholderImage : '${containerRegistry}/tangle-worker:${imageTag}'

var redisConnectionString = 'tangle-redis:6379'
var redisUrl = 'redis://${redisConnectionString}'

var apiSecretEnvVars = [
  { name: 'postgres-conn', envName: 'ConnectionStrings__DefaultConnection' }
  { name: 'blob-conn', envName: 'Media__ConnectionString' }
  { name: 'jwt-secret', envName: 'Jwt__Secret' }
  { name: 'worker-callback', envName: 'Media__WorkerCallbackSecret' }
  { name: 'metrics-secret', envName: 'Metrics__ScrapeSecret' }
  { name: 'places-api-key', envName: 'Places__ApiKey' }
  { name: 'appinsights-conn', envName: 'APPLICATIONINSIGHTS_CONNECTION_STRING' }
]

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

module postgresStorage 'modules/environment-storage.bicep' = {
  name: 'postgres-env-storage'
  params: {
    managedEnvironmentName: containerAppsEnv.outputs.name
    storageAccountName: storage.outputs.accountName
    storageAccountKey: storage.outputs.accountKey
  }
}

module postgres 'modules/infra-container.bicep' = {
  name: 'infra-postgres'
  params: {
    name: 'tangle-postgres'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: postgresImage
    minReplicas: 1
    maxReplicas: 1
    tcpProbePort: 5432
    environmentStorageName: postgresStorage.outputs.storageBindingName
    volumeMountPath: '/var/lib/postgresql/data'
    tags: tags
    envVars: [
      { name: 'POSTGRES_USER', value: postgresAdminLogin }
      { name: 'POSTGRES_DB', value: 'tangledb' }
      { name: 'PGDATA', value: '/var/lib/postgresql/data/pgdata' }
    ]
    secretEnvVars: [
      { name: 'postgres-password', envName: 'POSTGRES_PASSWORD', value: postgresAdminPassword }
    ]
  }
}

module redis 'modules/infra-container.bicep' = {
  name: 'infra-redis'
  params: {
    name: 'tangle-redis'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: redisImage
    minReplicas: 1
    maxReplicas: 1
    tcpProbePort: 6379
    tags: tags
    envVars: []
    secretEnvVars: []
  }
}

module api 'modules/container-app.bicep' = {
  name: 'container-app-api'
  params: {
    name: 'tangle-api'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: apiImage
    targetPort: usePlaceholderImages ? 80 : 8080
    enableIngress: true
    externalIngress: false
    minReplicas: apiMinReplicas
    maxReplicas: 3
    healthCheckPath: usePlaceholderImages ? '' : '/health'
    registryLoginServer: 'ghcr.io'
    registryUsername: registryUsername
    registryPassword: registryPassword
    tags: tags
    envVars: [
      { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
      { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
      { name: 'Redis__Enabled', value: 'true' }
      { name: 'Redis__ConnectionString', value: redisConnectionString }
      { name: 'Media__Enabled', value: 'true' }
      { name: 'Media__ContainerName', value: storage.outputs.containerName }
      { name: 'Media__PublicBlobEndpoint', value: storage.outputs.blobEndpoint }
      { name: 'Metrics__RequireScrapeSecret', value: 'true' }
    ]
    secretEnvVars: apiSecretEnvVars
  }
}

module web 'modules/container-app.bicep' = {
  name: 'container-app-web'
  params: {
    name: 'tangle-web'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: webImage
    targetPort: 80
    enableIngress: true
    externalIngress: true
    ingressTransport: 'auto'
    minReplicas: webMinReplicas
    maxReplicas: 3
    healthCheckPath: ''
    registryLoginServer: 'ghcr.io'
    registryUsername: registryUsername
    registryPassword: registryPassword
    tags: tags
    envVars: [
      { name: 'TANGLE_API_UPSTREAM', value: 'tangle-api:${usePlaceholderImages ? 80 : 8080}' }
    ]
    secretEnvVars: []
  }
}

module workerChat 'modules/container-app.bicep' = {
  name: 'container-app-worker-chat'
  params: {
    name: 'tangle-worker-chat'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: workerImage
    targetPort: 9090
    enableIngress: false
    minReplicas: workerMinReplicas
    maxReplicas: 2
    registryLoginServer: 'ghcr.io'
    registryUsername: registryUsername
    registryPassword: registryPassword
    tags: tags
    envVars: [
      { name: 'REDIS_URL', value: redisUrl }
      { name: 'WORKER_STREAM_PREFIX', value: 'tangle:queue:' }
      { name: 'WORKER_STREAM_KEY', value: 'chat.message.created' }
      { name: 'WORKER_CONSUMER_GROUP', value: 'tangle-workers' }
      { name: 'WORKER_METRICS_PORT', value: '9090' }
      { name: 'RUST_LOG', value: 'info' }
    ]
    secretEnvVars: []
  }
}

module workerMedia 'modules/container-app.bicep' = {
  name: 'container-app-worker-media'
  params: {
    name: 'tangle-worker-media'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: workerImage
    targetPort: 9090
    enableIngress: false
    minReplicas: workerMinReplicas
    maxReplicas: 2
    registryLoginServer: 'ghcr.io'
    registryUsername: registryUsername
    registryPassword: registryPassword
    tags: tags
    envVars: [
      { name: 'REDIS_URL', value: redisUrl }
      { name: 'WORKER_STREAM_PREFIX', value: 'tangle:queue:' }
      { name: 'WORKER_STREAM_KEY', value: 'media.uploaded' }
      { name: 'WORKER_CONSUMER_GROUP', value: 'tangle-workers' }
      { name: 'API_BASE_URL', value: 'http://tangle-api:${usePlaceholderImages ? 80 : 8080}' }
      { name: 'MEDIA_CONTAINER_NAME', value: storage.outputs.containerName }
      { name: 'WORKER_METRICS_PORT', value: '9090' }
      { name: 'RUST_LOG', value: 'info' }
    ]
    secretEnvVars: [
      { name: 'blob-conn', envName: 'AZURE_STORAGE_CONNECTION_STRING' }
      { name: 'worker-callback', envName: 'WORKER_CALLBACK_SECRET' }
    ]
  }
}

module workerLocation 'modules/container-app.bicep' = {
  name: 'container-app-worker-location'
  params: {
    name: 'tangle-worker-location'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: workerImage
    targetPort: 9090
    enableIngress: false
    minReplicas: workerMinReplicas
    maxReplicas: 2
    registryLoginServer: 'ghcr.io'
    registryUsername: registryUsername
    registryPassword: registryPassword
    tags: tags
    envVars: [
      { name: 'REDIS_URL', value: redisUrl }
      { name: 'WORKER_STREAM_PREFIX', value: 'tangle:queue:' }
      { name: 'WORKER_STREAM_KEY', value: 'location.cluster' }
      { name: 'WORKER_CONSUMER_GROUP', value: 'tangle-workers' }
      { name: 'API_BASE_URL', value: 'http://tangle-api:${usePlaceholderImages ? 80 : 8080}' }
      { name: 'WORKER_METRICS_PORT', value: '9090' }
      { name: 'RUST_LOG', value: 'info' }
    ]
    secretEnvVars: []
  }
}

module migrateJob 'modules/migrate-job.bicep' = {
  name: 'migrate-job'
  params: {
    name: 'tangle-migrate'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: apiImage
    registryLoginServer: 'ghcr.io'
    registryUsername: registryUsername
    registryPassword: registryPassword
    tags: tags
    envVars: [
      { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
    ]
    secretEnvVars: [
      { name: 'postgres-conn', envName: 'ConnectionStrings__DefaultConnection' }
    ]
  }
}

output webUrl string = 'https://${web.outputs.fqdn}'
output postgresAppName string = postgres.outputs.name
output redisAppName string = redis.outputs.name
output blobEndpoint string = storage.outputs.blobEndpoint
output appInsightsConnectionString string = appInsights.outputs.connectionString
output containerAppsEnvironmentId string = containerAppsEnv.outputs.id
output migrateJobName string = migrateJob.outputs.name
output containerAppNames object = {
  api: api.outputs.name
  web: web.outputs.name
  postgres: postgres.outputs.name
  redis: redis.outputs.name
  workerChat: workerChat.outputs.name
  workerMedia: workerMedia.outputs.name
  workerLocation: workerLocation.outputs.name
}
