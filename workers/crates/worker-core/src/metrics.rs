use ::metrics::{counter, gauge};
use anyhow::{Context, Result};
use metrics_exporter_prometheus::PrometheusBuilder;
use redis::aio::ConnectionManager;
use redis::AsyncCommands;
use std::convert::Infallible;
use std::sync::Arc;
use tracing::info;

use crate::config::Config;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum JobOutcome {
    Success,
    Failure,
    Malformed,
    Dlq,
}

impl JobOutcome {
    pub const fn as_str(self) -> &'static str {
        match self {
            Self::Success => "success",
            Self::Failure => "failure",
            Self::Malformed => "malformed",
            Self::Dlq => "dlq",
        }
    }
}

pub fn init(port: u16) -> Result<()> {
    let scrape_secret = std::env::var("METRICS_SCRAPE_SECRET")
        .ok()
        .filter(|value| !value.is_empty());
    let handle = PrometheusBuilder::new()
        .install_recorder()
        .map_err(|err| anyhow::anyhow!("install Prometheus metrics recorder: {err}"))?;

    let secret = scrape_secret.clone();
    tokio::spawn(async move {
        if let Err(err) = serve_metrics(port, secret, handle).await {
            tracing::error!(error = %err, "metrics server failed");
        }
    });

    info!(port = port, auth = scrape_secret.is_some(), "Prometheus metrics listening");
    Ok(())
}

async fn serve_metrics(
    port: u16,
    scrape_secret: Option<String>,
    handle: metrics_exporter_prometheus::PrometheusHandle,
) -> Result<()> {
    use http_body_util::Full;
    use hyper::body::Bytes;
    use hyper::server::conn::http1;
    use hyper::service::service_fn;
    use hyper::{Request, Response, StatusCode};
    use hyper_util::rt::TokioIo;
    use tokio::net::TcpListener;

    let secret = Arc::new(scrape_secret);
    let listener = TcpListener::bind(("0.0.0.0", port))
        .await
        .with_context(|| format!("bind metrics listener on port {port}"))?;

    loop {
        let (stream, _) = listener.accept().await.context("accept metrics connection")?;
        let io = TokioIo::new(stream);
        let handle = handle.clone();
        let secret = Arc::clone(&secret);

        tokio::spawn(async move {
            let service = service_fn(move |req: Request<hyper::body::Incoming>| {
                let handle = handle.clone();
                let secret = Arc::clone(&secret);
                async move {
                    if !authorize_metrics_request(req.headers(), secret.as_deref()) {
                        return Ok::<_, Infallible>(Response::builder()
                            .status(StatusCode::UNAUTHORIZED)
                            .body(Full::new(Bytes::from_static(b"Unauthorized")))
                            .unwrap());
                    }

                    if req.uri().path() != "/metrics" {
                        return Ok(Response::builder()
                            .status(StatusCode::NOT_FOUND)
                            .body(Full::new(Bytes::from_static(b"Not Found")))
                            .unwrap());
                    }

                    Ok(Response::builder()
                        .status(StatusCode::OK)
                        .header("Content-Type", "text/plain; version=0.0.4")
                        .body(Full::new(Bytes::from(handle.render())))
                        .unwrap())
                }
            });

            if let Err(err) = http1::Builder::new()
                .serve_connection(io, service)
                .await
            {
                tracing::debug!(error = %err, "metrics connection closed");
            }
        });
    }
}

fn authorize_metrics_request(
    headers: &hyper::HeaderMap,
    expected_secret: Option<&str>,
) -> bool {
    match expected_secret {
        None => true,
        Some(expected) => headers
            .get("x-metrics-secret")
            .and_then(|value| value.to_str().ok())
            .is_some_and(|value| value == expected),
    }
}

pub fn record_callback_request(code: &str) {
    counter!(
        "tangle_worker_callback_requests_total",
        "code" => code.to_owned()
    )
    .increment(1);
}

pub fn record_job_processed(stream_key: &str, outcome: JobOutcome) {
    counter!(
        "tangle_worker_jobs_processed_total",
        "stream_key" => stream_key.to_owned(),
        "outcome" => outcome.as_str()
    )
    .increment(1);
}

pub async fn refresh_queue_gauges(conn: &mut ConnectionManager, config: &Config) -> Result<()> {
    let stream = config.full_stream_key();
    let pending: redis::streams::StreamPendingReply = match conn
        .xpending(&stream, &config.stream.consumer_group)
        .await
    {
        Ok(pending) => pending,
        Err(err) if crate::error::is_transient_redis_error(&err) => return Ok(()),
        Err(err) => {
            return Err(err).with_context(|| format!("XPENDING summary on stream {stream}"));
        }
    };

    gauge!(
        "tangle_worker_pending_messages",
        "stream_key" => config.stream.stream_key.clone()
    )
    .set(pending.count() as f64);

    let dlq_stream = config.dlq_stream_key();
    let dlq_length: usize = match conn.xlen(&dlq_stream).await {
        Ok(length) => length,
        Err(err) if crate::error::is_transient_redis_error(&err) => return Ok(()),
        Err(err) => {
            return Err(err).with_context(|| format!("XLEN on stream {dlq_stream}"));
        }
    };

    gauge!(
        "tangle_worker_dlq_length",
        "stream_key" => config.stream.stream_key.clone()
    )
    .set(dlq_length as f64);

    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use hyper::HeaderMap;

    #[test]
    fn authorize_metrics_request_allows_when_secret_unset() {
        let headers = HeaderMap::new();
        assert!(authorize_metrics_request(&headers, None));
    }

    #[test]
    fn authorize_metrics_request_requires_matching_header() {
        let mut headers = HeaderMap::new();
        assert!(!authorize_metrics_request(&headers, Some("secret")));

        headers.insert("x-metrics-secret", "wrong".parse().unwrap());
        assert!(!authorize_metrics_request(&headers, Some("secret")));

        headers.insert("x-metrics-secret", "secret".parse().unwrap());
        assert!(authorize_metrics_request(&headers, Some("secret")));
    }
}
