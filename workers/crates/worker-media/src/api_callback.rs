use anyhow::{Context, Result};
use reqwest::StatusCode;
use serde::Serialize;
use worker_core::callback::{CallbackAttemptOutcome, CALLBACK_HEADER};
use worker_core::callback;
use worker_core::Config;

/// Matches API `MediaProcessedRequestDto.FailureReason` `[StringLength(2000)]`.
const MAX_FAILURE_REASON_LEN: usize = 2000;
const TRUNCATION_ELLIPSIS: &str = "…";

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
    let failure_reason = truncate_failure_reason(failure_reason);
    let body = MediaProcessedRequest {
        processed_object_key: None,
        stored_size_bytes: None,
        failure_reason: Some(failure_reason.as_str()),
    };
    send_callback(client, config, media_asset_id, &body).await
}

async fn send_callback(
    client: &reqwest::Client,
    config: &Config,
    media_asset_id: i64,
    body: &MediaProcessedRequest<'_>,
) -> Result<()> {
    let url = format!(
        "{}/internal/media/{media_asset_id}/processed",
        config.callback.api_base_url.trim_end_matches('/')
    );
    let callback_config = config.callback.clone();

    callback::send_with_retry(&callback_config, "media processed", || {
        try_send_callback(client, &callback_config, &url, body)
    })
    .await
}

async fn try_send_callback(
    client: &reqwest::Client,
    config: &worker_core::CallbackConfig,
    url: &str,
    body: &MediaProcessedRequest<'_>,
) -> CallbackAttemptOutcome {
    let response = match client
        .patch(url)
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

fn truncate_failure_reason(reason: &str) -> String {
    if reason.chars().count() <= MAX_FAILURE_REASON_LEN {
        return reason.to_owned();
    }

    let keep = MAX_FAILURE_REASON_LEN.saturating_sub(TRUNCATION_ELLIPSIS.chars().count());
    let truncated: String = reason.chars().take(keep).collect();
    format!("{truncated}{TRUNCATION_ELLIPSIS}")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn truncate_failure_reason_leaves_short_reasons_unchanged() {
        let reason = "compression failed";
        assert_eq!(truncate_failure_reason(reason), reason);
    }

    #[test]
    fn truncate_failure_reason_caps_at_api_limit() {
        let reason = "x".repeat(2500);
        let truncated = truncate_failure_reason(&reason);

        assert!(truncated.chars().count() <= MAX_FAILURE_REASON_LEN);
        assert!(truncated.ends_with(TRUNCATION_ELLIPSIS));
    }
}
