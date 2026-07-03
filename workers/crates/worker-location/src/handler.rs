use anyhow::{bail, Context, Result};
use reqwest::StatusCode;
use serde::{Deserialize, Serialize};
use worker_core::callback::{CallbackAttemptOutcome, CALLBACK_HEADER};
use worker_core::callback;
use worker_core::Config;

use crate::cluster::{ClusterPoint, PinPoint};
use crate::job::LocationClusterJob;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct ClusterPointResponse {
    id: i64,
    latitude: f64,
    longitude: f64,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct ClusterStoreRequest {
    min_latitude: f64,
    max_latitude: f64,
    min_longitude: f64,
    max_longitude: f64,
    zoom: i32,
    clusters: Vec<ClusterPayload>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct ClusterPayload {
    latitude: f64,
    longitude: f64,
    pin_count: i32,
    sample_pin_id: Option<i64>,
}

pub async fn handle(
    job: &LocationClusterJob,
    config: &Config,
    http: &reqwest::Client,
) -> Result<()> {
    let points = fetch_cluster_points(http, config, job).await?;
    let clusters = crate::cluster::cluster_pins(&points, job.zoom as u32);
    store_clusters(http, config, job, &clusters).await
}

async fn fetch_cluster_points(
    client: &reqwest::Client,
    config: &Config,
    job: &LocationClusterJob,
) -> Result<Vec<PinPoint>> {
    let url = format!(
        "{}/internal/location/cluster-points?minLatitude={}&maxLatitude={}&minLongitude={}&maxLongitude={}",
        config.callback.api_base_url.trim_end_matches('/'),
        job.min_latitude,
        job.max_latitude,
        job.min_longitude,
        job.max_longitude
    );

    let response = client
        .get(url)
        .header(CALLBACK_HEADER, &config.callback.worker_callback_secret)
        .send()
        .await
        .context("fetch cluster points")?;

    if response.status() == StatusCode::NO_CONTENT {
        return Ok(Vec::new());
    }

    if !response.status().is_success() {
        bail!("cluster points request failed with status {}", response.status());
    }

    let body: Vec<ClusterPointResponse> = response.json().await.context("decode cluster points")?;
    Ok(body
        .into_iter()
        .map(|point| PinPoint {
            id: point.id,
            latitude: point.latitude,
            longitude: point.longitude,
        })
        .collect())
}

async fn store_clusters(
    client: &reqwest::Client,
    config: &Config,
    job: &LocationClusterJob,
    clusters: &[ClusterPoint],
) -> Result<()> {
    let payloads: Vec<ClusterPayload> = clusters
        .iter()
        .map(|cluster| -> Result<ClusterPayload> {
            Ok(ClusterPayload {
                latitude: cluster.latitude,
                longitude: cluster.longitude,
                pin_count: i32::try_from(cluster.pin_count).context("pin count exceeds i32")?,
                sample_pin_id: cluster.sample_pin_id,
            })
        })
        .collect::<Result<_>>()?;

    let body = ClusterStoreRequest {
        min_latitude: job.min_latitude,
        max_latitude: job.max_latitude,
        min_longitude: job.min_longitude,
        max_longitude: job.max_longitude,
        zoom: job.zoom,
        clusters: payloads,
    };

    let url = format!(
        "{}/internal/location/clusters",
        config.callback.api_base_url.trim_end_matches('/')
    );
    let callback_config = config.callback.clone();

    callback::send_with_retry(&callback_config, "location cluster", || {
        try_store_clusters(client, &callback_config, &url, &body)
    })
    .await
}

async fn try_store_clusters(
    client: &reqwest::Client,
    config: &worker_core::CallbackConfig,
    url: &str,
    body: &ClusterStoreRequest,
) -> CallbackAttemptOutcome {
    let response = match client
        .put(url)
        .header(CALLBACK_HEADER, &config.worker_callback_secret)
        .json(body)
        .send()
        .await
    {
        Ok(response) => response,
        Err(_) => return CallbackAttemptOutcome::TransportError,
    };

    if response.status() == StatusCode::NO_CONTENT {
        return CallbackAttemptOutcome::Success;
    }

    CallbackAttemptOutcome::HttpError(response.status())
}
