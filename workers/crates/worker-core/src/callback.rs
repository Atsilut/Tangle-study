use std::future::Future;
use std::time::Duration;

use anyhow::{bail, Result};
use reqwest::StatusCode;
use tracing::warn;

use crate::config::CallbackConfig;
use crate::metrics;

pub const CALLBACK_HEADER: &str = "X-Worker-Callback-Secret";

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CallbackAttemptOutcome {
    Success,
    HttpError(StatusCode),
    TransportError,
}

pub async fn send_with_retry<F, Fut>(config: &CallbackConfig, log_context: &str, mut send_once: F) -> Result<()>
where
    F: FnMut() -> Fut,
    Fut: Future<Output = CallbackAttemptOutcome>,
{
    if config.worker_callback_secret.trim().is_empty() {
        bail!("WORKER_CALLBACK_SECRET is not configured");
    }

    let max_attempts = config.max_retries.max(1);
    let mut last_outcome: Option<CallbackAttemptOutcome> = None;

    for attempt in 1..=max_attempts {
        let outcome = send_once().await;
        match outcome {
            CallbackAttemptOutcome::Success => {
                metrics::record_callback_request("204");
                return Ok(());
            }
            CallbackAttemptOutcome::HttpError(status)
                if is_retryable_http_status(status) && attempt < max_attempts =>
            {
                let delay_ms = config.retry_base_ms.saturating_mul(2u64.pow(attempt - 1));
                warn!(
                    context = log_context,
                    attempt = attempt,
                    max_attempts = max_attempts,
                    status = %status,
                    retry_after_ms = delay_ms,
                    "callback failed; retrying"
                );
                tokio::time::sleep(Duration::from_millis(delay_ms)).await;
                last_outcome = Some(CallbackAttemptOutcome::HttpError(status));
            }
            CallbackAttemptOutcome::HttpError(status) => {
                metrics::record_callback_request(&status.as_u16().to_string());
                bail!("{log_context} callback failed with status {status}");
            }
            CallbackAttemptOutcome::TransportError if attempt < max_attempts => {
                let delay_ms = config.retry_base_ms.saturating_mul(2u64.pow(attempt - 1));
                warn!(
                    context = log_context,
                    attempt = attempt,
                    max_attempts = max_attempts,
                    retry_after_ms = delay_ms,
                    "callback failed; retrying"
                );
                tokio::time::sleep(Duration::from_millis(delay_ms)).await;
                last_outcome = Some(CallbackAttemptOutcome::TransportError);
            }
            CallbackAttemptOutcome::TransportError => {
                metrics::record_callback_request("transport_error");
                bail!("send {log_context} callback");
            }
        }
    }

    match last_outcome.expect("callback loop exits only after recording a retryable error") {
        CallbackAttemptOutcome::HttpError(status) => {
            metrics::record_callback_request(&status.as_u16().to_string());
            bail!("{log_context} callback failed with status {status}");
        }
        CallbackAttemptOutcome::TransportError => {
            metrics::record_callback_request("transport_error");
            bail!("send {log_context} callback");
        }
        CallbackAttemptOutcome::Success => unreachable!("success returns early"),
    }
}

pub fn is_retryable_http_status(status: StatusCode) -> bool {
    status.is_server_error() || status == StatusCode::TOO_MANY_REQUESTS
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::atomic::{AtomicU32, Ordering};
    use std::sync::Arc;

    fn test_callback_config(max_retries: u32) -> CallbackConfig {
        CallbackConfig {
            api_base_url: "http://api:8080".to_owned(),
            worker_callback_secret: "secret".to_owned(),
            timeout_ms: 1_000,
            connect_timeout_ms: 1_000,
            max_retries,
            retry_base_ms: 0,
        }
    }

    #[test]
    fn is_retryable_http_status_matches_server_errors_and_429() {
        assert!(is_retryable_http_status(StatusCode::INTERNAL_SERVER_ERROR));
        assert!(is_retryable_http_status(StatusCode::TOO_MANY_REQUESTS));
        assert!(!is_retryable_http_status(StatusCode::NOT_FOUND));
        assert!(!is_retryable_http_status(StatusCode::BAD_REQUEST));
    }

    #[tokio::test]
    async fn send_with_retry_succeeds_on_first_attempt() {
        let config = test_callback_config(3);
        send_with_retry(&config, "test", || async { CallbackAttemptOutcome::Success })
            .await
            .unwrap();
    }

    #[tokio::test]
    async fn send_with_retry_retries_retryable_http_error() {
        let config = test_callback_config(3);
        let attempts = Arc::new(AtomicU32::new(0));
        let attempts_for_closure = Arc::clone(&attempts);

        send_with_retry(&config, "test", || {
            let attempts = Arc::clone(&attempts_for_closure);
            async move {
                let attempt = attempts.fetch_add(1, Ordering::SeqCst) + 1;
                if attempt == 1 {
                    CallbackAttemptOutcome::HttpError(StatusCode::INTERNAL_SERVER_ERROR)
                } else {
                    CallbackAttemptOutcome::Success
                }
            }
        })
        .await
        .unwrap();

        assert_eq!(attempts.load(Ordering::SeqCst), 2);
    }

    #[tokio::test]
    async fn send_with_retry_fails_immediately_on_non_retryable_status() {
        let config = test_callback_config(3);
        let attempts = Arc::new(AtomicU32::new(0));
        let attempts_for_closure = Arc::clone(&attempts);

        let err = send_with_retry(&config, "test", || {
            let attempts = Arc::clone(&attempts_for_closure);
            async move {
                attempts.fetch_add(1, Ordering::SeqCst);
                CallbackAttemptOutcome::HttpError(StatusCode::NOT_FOUND)
            }
        })
        .await
        .unwrap_err();

        assert_eq!(attempts.load(Ordering::SeqCst), 1);
        assert!(err.to_string().contains("404"));
    }

    #[tokio::test]
    async fn send_with_retry_exhausts_transport_retries() {
        let config = test_callback_config(2);
        let attempts = Arc::new(AtomicU32::new(0));
        let attempts_for_closure = Arc::clone(&attempts);

        let err = send_with_retry(&config, "test", || {
            let attempts = Arc::clone(&attempts_for_closure);
            async move {
                attempts.fetch_add(1, Ordering::SeqCst);
                CallbackAttemptOutcome::TransportError
            }
        })
        .await
        .unwrap_err();

        assert_eq!(attempts.load(Ordering::SeqCst), 2);
        assert!(err.to_string().contains("send test callback"));
    }

    #[tokio::test]
    async fn send_with_retry_requires_callback_secret() {
        let mut config = test_callback_config(1);
        config.worker_callback_secret = "   ".to_owned();

        let err = send_with_retry(&config, "test", || async { CallbackAttemptOutcome::Success })
            .await
            .unwrap_err();

        assert!(err
            .to_string()
            .contains("WORKER_CALLBACK_SECRET is not configured"));
    }
}
