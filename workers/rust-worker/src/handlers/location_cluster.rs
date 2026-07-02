use std::time::Duration;

use anyhow::{bail, Context, Result};
use reqwest::StatusCode;
use serde::{Deserialize, Serialize};
use tracing::warn;

use worker_core::config::Config;
use worker_core::job::LocationClusterJob;
use worker_core::metrics;

use crate::cluster::ClusterPoint;

pub const CALLBACK_HEADER: &str = "X-Worker-Callback-Secret";

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

enum CallbackAttemptOutcome {
    Success,
    HttpError(StatusCode),
    TransportError,
}

pub async fn handle(
    job: &LocationClusterJob,
    config: &Config,
    http: &reqwest::Client,
) -> Result<()> {
    job.validate()?;

    let points = fetch_cluster_points(http, config, job).await?;
    let clusters = crate::cluster::cluster_pins(&points, job.zoom as u32);
    store_clusters(http, config, job, &clusters).await
}

async fn fetch_cluster_points(
    client: &reqwest::Client,
    config: &Config,
    job: &LocationClusterJob,
) -> Result<Vec<crate::cluster::PinPoint>> {
    let url = format!(
        "{}/internal/location/cluster-points?minLatitude={}&maxLatitude={}&minLongitude={}&maxLongitude={}",
        config.api_base_url.trim_end_matches('/'),
        job.min_latitude,
        job.max_latitude,
        job.min_longitude,
        job.max_longitude
    );

    let response = client
        .get(url)
        .header(CALLBACK_HEADER, &config.worker_callback_secret)
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
        .map(|point| crate::cluster::PinPoint {
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
    if config.worker_callback_secret.trim().is_empty() {
        bail!("WORKER_CALLBACK_SECRET is not configured");
    }

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
        config.api_base_url.trim_end_matches('/')
    );

    let max_attempts = config.callback_max_retries.max(1);
    let mut last_outcome: Option<CallbackAttemptOutcome> = None;

    for attempt in 1..=max_attempts {
        let outcome = try_store_clusters(client, config, &url, &body).await;
        match outcome {
            CallbackAttemptOutcome::Success => {
                metrics::record_callback_request("204");
                return Ok(());
            }
            CallbackAttemptOutcome::HttpError(status)
                if is_retryable_http_status(status) && attempt < max_attempts =>
            {
                let delay_ms = config.callback_retry_base_ms.saturating_mul(2u64.pow(attempt - 1));
                warn!(
                    zoom = job.zoom,
                    attempt = attempt,
                    max_attempts = max_attempts,
                    status = %status,
                    retry_after_ms = delay_ms,
                    "location cluster callback failed; retrying"
                );
                tokio::time::sleep(Duration::from_millis(delay_ms)).await;
                last_outcome = Some(CallbackAttemptOutcome::HttpError(status));
            }
            CallbackAttemptOutcome::HttpError(status) => {
                metrics::record_callback_request(&status.as_u16().to_string());
                bail!("location cluster callback failed with status {status}");
            }
            CallbackAttemptOutcome::TransportError if attempt < max_attempts => {
                let delay_ms = config.callback_retry_base_ms.saturating_mul(2u64.pow(attempt - 1));
                warn!(
                    zoom = job.zoom,
                    attempt = attempt,
                    max_attempts = max_attempts,
                    retry_after_ms = delay_ms,
                    "location cluster callback failed; retrying"
                );
                tokio::time::sleep(Duration::from_millis(delay_ms)).await;
                last_outcome = Some(CallbackAttemptOutcome::TransportError);
            }
            CallbackAttemptOutcome::TransportError => {
                metrics::record_callback_request("transport_error");
                bail!("send location cluster callback");
            }
        }
    }

    match last_outcome.expect("callback loop exits only after recording a retryable error") {
        CallbackAttemptOutcome::HttpError(status) => {
            metrics::record_callback_request(&status.as_u16().to_string());
            bail!("location cluster callback failed with status {status}");
        }
        CallbackAttemptOutcome::TransportError => {
            metrics::record_callback_request("transport_error");
            bail!("send location cluster callback");
        }
        CallbackAttemptOutcome::Success => unreachable!("success returns early"),
    }
}

async fn try_store_clusters(
    client: &reqwest::Client,
    config: &Config,
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

fn is_retryable_http_status(status: StatusCode) -> bool {
    status.is_server_error() || status == StatusCode::TOO_MANY_REQUESTS
}
