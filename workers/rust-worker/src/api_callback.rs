use anyhow::{bail, Context, Result};
use reqwest::StatusCode;
use serde::Serialize;

use crate::config::Config;

pub const CALLBACK_HEADER: &str = "X-Worker-Callback-Secret";

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct MediaProcessedRequest<'a> {
    processed_object_key: Option<&'a str>,
    stored_size_bytes: Option<i64>,
    failure_reason: Option<&'a str>,
}

pub async fn report_success(
    client: &reqwest::Client,
    config: &Config,
    media_asset_id: i64,
    processed_object_key: &str,
    stored_size_bytes: u64,
) -> Result<()> {
    let body = MediaProcessedRequest {
        processed_object_key: Some(processed_object_key),
        stored_size_bytes: Some(i64::try_from(stored_size_bytes).context("stored size exceeds i64")?),
        failure_reason: None,
    };
    send_callback(client, config, media_asset_id, &body).await
}

pub async fn report_failure(
    client: &reqwest::Client,
    config: &Config,
    media_asset_id: i64,
    failure_reason: &str,
) -> Result<()> {
    let body = MediaProcessedRequest {
        processed_object_key: None,
        stored_size_bytes: None,
        failure_reason: Some(failure_reason),
    };
    send_callback(client, config, media_asset_id, &body).await
}

async fn send_callback(
    client: &reqwest::Client,
    config: &Config,
    media_asset_id: i64,
    body: &MediaProcessedRequest<'_>,
) -> Result<()> {
    if config.worker_callback_secret.trim().is_empty() {
        bail!("WORKER_CALLBACK_SECRET is not configured");
    }

    let url = format!(
        "{}/internal/media/{media_asset_id}/processed",
        config.api_base_url.trim_end_matches('/')
    );

    let response = client
        .patch(url)
        .header(CALLBACK_HEADER, &config.worker_callback_secret)
        .json(body)
        .send()
        .await
        .context("send media processed callback")?;

    if response.status() == StatusCode::NO_CONTENT {
        return Ok(());
    }

    let status = response.status();
    let detail = response.text().await.unwrap_or_default();
    bail!("media processed callback failed with status {status}: {detail}");
}
