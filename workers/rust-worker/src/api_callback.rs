use std::time::Duration;

use anyhow::{bail, Context, Result};
use reqwest::StatusCode;
use serde::Serialize;
use tracing::warn;

use crate::config::Config;

pub const CALLBACK_HEADER: &str = "X-Worker-Callback-Secret";

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct MediaProcessedRequest<'a> {
    processed_object_key: Option<&'a str>,
    stored_size_bytes: Option<i64>,
    failure_reason: Option<&'a str>,
}

/// Builds a shared HTTP client with configured connect and request timeouts.
pub fn build_client(config: &Config) -> Result<reqwest::Client> {
    reqwest::Client::builder()
        .connect_timeout(Duration::from_millis(config.callback_connect_timeout_ms))
        .timeout(Duration::from_millis(config.callback_timeout_ms))
        .build()
        .context("build media callback HTTP client")
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

    let max_attempts = config.callback_max_retries.max(1);
    let mut last_err: Option<anyhow::Error> = None;

    for attempt in 1..=max_attempts {
        match try_send_callback(client, config, &url, body).await {
            Ok(()) => return Ok(()),
            Err(err) if is_retryable_callback_error(&err) && attempt < max_attempts => {
                let delay_ms = config.callback_retry_base_ms.saturating_mul(2u64.pow(attempt - 1));
                warn!(
                    media_asset_id = media_asset_id,
                    attempt = attempt,
                    max_attempts = max_attempts,
                    retry_after_ms = delay_ms,
                    error = %err,
                    "media processed callback failed; retrying"
                );
                tokio::time::sleep(Duration::from_millis(delay_ms)).await;
                last_err = Some(err);
            }
            Err(err) => return Err(err),
        }
    }

    Err(last_err.expect("callback loop exits only after recording a retryable error"))
}

async fn try_send_callback(
    client: &reqwest::Client,
    config: &Config,
    url: &str,
    body: &MediaProcessedRequest<'_>,
) -> Result<()> {
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

fn is_retryable_callback_error(err: &anyhow::Error) -> bool {
    if err.chain().any(|cause| cause.is::<reqwest::Error>()) {
        return true;
    }

    err.chain().any(|cause| {
        let message = cause.to_string();
        status_code_from_message(&message)
            .is_some_and(|status| status.is_server_error() || status == StatusCode::TOO_MANY_REQUESTS)
    })
}

fn status_code_from_message(message: &str) -> Option<StatusCode> {
    let prefix = "media processed callback failed with status ";
    let rest = message.strip_prefix(prefix)?;
    let status_token = rest.split(':').next()?.trim();
    status_token.parse().ok()
}
