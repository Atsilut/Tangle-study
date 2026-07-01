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

param redisImage string = 'redis:8-alpine'

param prometheusImage string = 'prom/prometheus:v3.12.0'

param grafanaImage string = 'grafana/grafana:13.0.2'

param postgresExporterImage string = 'prometheuscommunity/postgres-exporter:v0.19.1'

param redisExporterImage string = 'oliver006/redis_exporter:v1.86.0'

param monitoringMinReplicas int = 1

param apiMinReplicas int = 1
param webMinReplicas int = 1
param workerMinReplicas int = 0

param tags object = {
  project: 'tangle-study'
  environment: environment
}

var namePrefix = '${baseName}${environment}'
var placeholderImage = 'mcr.microsoft.com/k8se/quickstart:latest'
var apiImage = usePlaceholderImages ? placeholderImage : '${containerRegistry}/tangle-study-api:${imageTag}'
var webImage = usePlaceholderImages ? placeholderImage : '${containerRegistry}/tangle-study-web:${imageTag}'
var workerImage = usePlaceholderImages ? placeholderImage : '${containerRegistry}/tangle-study-worker:${imageTag}'
var resolvedPrometheusImage = usePlaceholderImages ? prometheusImage : '${containerRegistry}/tangle-study-prometheus:${imageTag}'
var resolvedGrafanaImage = usePlaceholderImages ? grafanaImage : '${containerRegistry}/tangle-study-grafana:${imageTag}'

var workerMetricsSecretEnvVars = [
  { name: 'metrics-secret', envName: 'METRICS_SCRAPE_SECRET' }
]

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

// Redis TCP ingress: short app name within the CAE (port 6379 implicit).
var redisInternalHost = 'tangle-study-redis'
var redisConnectionString = redisInternalHost
var redisUrl = 'redis://${redisInternalHost}'
var apiAppHost = 'tangle-study-api'

// ACA HTTP ingress: short app names route on port 80 (ingress front door), not
// the container targetPort. Do not append :8080 — callers time out on pod IP.
// NOTE: Bootstrap-only (azure-deploy-infra.sh). CD reads TANGLE_API_UPSTREAM
// from parameters.prod.json; keep both in sync.
var apiAppUpstream = apiAppHost
var apiAppBaseUrl = 'http://${apiAppHost}'
var prometheusInternalUrl = 'http://tangle-study-prometheus'

module redis 'modules/infra-container.bicep' = {
  name: 'infra-redis'
  params: {
    name: 'tangle-study-redis'
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
    name: 'tangle-study-api'
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
    name: 'tangle-study-web'
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
      { name: 'TANGLE_API_UPSTREAM', value: apiAppUpstream }
    ]
    secretEnvVars: []
  }
}

module workerChat 'modules/container-app.bicep' = {
  name: 'container-app-worker-chat'
  params: {
    name: 'tangle-study-worker-chat'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: workerImage
    targetPort: 9090
    enableIngress: true
    externalIngress: false
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
      { name: 'WORKER_CONSUMER_GROUP', value: 'tangle-study-workers' }
      { name: 'WORKER_METRICS_PORT', value: '9090' }
      { name: 'RUST_LOG', value: 'info' }
    ]
    secretEnvVars: workerMetricsSecretEnvVars
  }
}

module workerMedia 'modules/container-app.bicep' = {
  name: 'container-app-worker-media'
  params: {
    name: 'tangle-study-worker-media'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: workerImage
    targetPort: 9090
    enableIngress: true
    externalIngress: false
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
      { name: 'WORKER_CONSUMER_GROUP', value: 'tangle-study-workers' }
      { name: 'API_BASE_URL', value: apiAppBaseUrl }
      { name: 'MEDIA_CONTAINER_NAME', value: storage.outputs.containerName }
      { name: 'WORKER_METRICS_PORT', value: '9090' }
      { name: 'RUST_LOG', value: 'info' }
    ]
    secretEnvVars: concat(workerMetricsSecretEnvVars, [
      { name: 'blob-conn', envName: 'AZURE_STORAGE_CONNECTION_STRING' }
      { name: 'worker-callback', envName: 'WORKER_CALLBACK_SECRET' }
    ])
  }
}

module workerLocation 'modules/container-app.bicep' = {
  name: 'container-app-worker-location'
  params: {
    name: 'tangle-study-worker-location'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: workerImage
    targetPort: 9090
    enableIngress: true
    externalIngress: false
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
      { name: 'WORKER_CONSUMER_GROUP', value: 'tangle-study-workers' }
      { name: 'API_BASE_URL', value: apiAppBaseUrl }
      { name: 'WORKER_METRICS_PORT', value: '9090' }
      { name: 'RUST_LOG', value: 'info' }
    ]
    secretEnvVars: workerMetricsSecretEnvVars
  }
}

module postgresExporter 'modules/container-app.bicep' = {
  name: 'container-app-postgres-exporter'
  params: {
    name: 'tangle-study-postgres-exporter'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: postgresExporterImage
    targetPort: 9187
    enableIngress: true
    externalIngress: false
    minReplicas: monitoringMinReplicas
    maxReplicas: 1
    healthCheckPath: ''
    registryLoginServer: ''
    registryUsername: ''
    registryPassword: ''
    tags: tags
    envVars: []
    secretEnvVars: [
      { name: 'postgres-dsn', envName: 'DATA_SOURCE_NAME' }
    ]
  }
}

module redisExporter 'modules/container-app.bicep' = {
  name: 'container-app-redis-exporter'
  params: {
    name: 'tangle-study-redis-exporter'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: redisExporterImage
    targetPort: 9121
    enableIngress: true
    externalIngress: false
    minReplicas: monitoringMinReplicas
    maxReplicas: 1
    healthCheckPath: ''
    registryLoginServer: ''
    registryUsername: ''
    registryPassword: ''
    tags: tags
    envVars: [
      { name: 'REDIS_ADDR', value: redisInternalHost }
    ]
    secretEnvVars: []
  }
}

module prometheus 'modules/container-app.bicep' = {
  name: 'container-app-prometheus'
  params: {
    name: 'tangle-study-prometheus'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: resolvedPrometheusImage
    targetPort: 9090
    enableIngress: true
    externalIngress: false
    minReplicas: monitoringMinReplicas
    maxReplicas: 1
    healthCheckPath: ''
    registryLoginServer: 'ghcr.io'
    registryUsername: registryUsername
    registryPassword: registryPassword
    tags: tags
    envVars: []
    secretEnvVars: [
      { name: 'metrics-secret', envName: 'METRICS_SCRAPE_SECRET' }
    ]
  }
}

module grafana 'modules/container-app.bicep' = {
  name: 'container-app-grafana'
  params: {
    name: 'tangle-study-grafana'
    location: location
    managedEnvironmentId: containerAppsEnv.outputs.id
    containerImage: resolvedGrafanaImage
    targetPort: 3000
    enableIngress: true
    externalIngress: true
    minReplicas: monitoringMinReplicas
    maxReplicas: 1
    healthCheckPath: ''
    registryLoginServer: 'ghcr.io'
    registryUsername: registryUsername
    registryPassword: registryPassword
    tags: tags
    envVars: [
      { name: 'GF_SECURITY_ADMIN_USER', value: 'admin' }
      { name: 'GF_USERS_ALLOW_SIGN_UP', value: 'false' }
      { name: 'PROMETHEUS_URL', value: prometheusInternalUrl }
    ]
    secretEnvVars: [
      { name: 'grafana-admin-password', envName: 'GF_SECURITY_ADMIN_PASSWORD' }
    ]
  }
}

module migrateJob 'modules/migrate-job.bicep' = {
  name: 'migrate-job'
  params: {
    name: 'tangle-study-migrate'
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
output grafanaUrl string = 'https://${grafana.outputs.fqdn}'
output prometheusInternalUrl string = prometheusInternalUrl
output redisAppName string = redis.outputs.name
output blobEndpoint string = storage.outputs.blobEndpoint
output appInsightsConnectionString string = appInsights.outputs.connectionString
output containerAppsEnvironmentId string = containerAppsEnv.outputs.id
output migrateJobName string = migrateJob.outputs.name
output containerAppNames object = {
  api: api.outputs.name
  web: web.outputs.name
  redis: redis.outputs.name
  workerChat: workerChat.outputs.name
  workerMedia: workerMedia.outputs.name
  workerLocation: workerLocation.outputs.name
  postgresExporter: postgresExporter.outputs.name
  redisExporter: redisExporter.outputs.name
  prometheus: prometheus.outputs.name
  grafana: grafana.outputs.name
}
