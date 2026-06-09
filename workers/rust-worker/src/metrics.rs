use ::metrics::{counter, gauge};
use anyhow::{Context, Result};
use metrics_exporter_prometheus::PrometheusBuilder;
use redis::aio::ConnectionManager;
use redis::AsyncCommands;
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
    PrometheusBuilder::new()
        .with_http_listener(([0, 0, 0, 0], port))
        .install()
        .map_err(|err| anyhow::anyhow!("install Prometheus metrics recorder: {err}"))?;
    info!(port = port, "Prometheus metrics listening");
    Ok(())
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
    let pending: redis::streams::StreamPendingReply = conn
        .xpending(&stream, &config.consumer_group)
        .await
        .with_context(|| format!("XPENDING summary on stream {stream}"))?;

    gauge!(
        "tangle_worker_pending_messages",
        "stream_key" => config.stream_key.clone()
    )
    .set(pending.count() as f64);

    let dlq_stream = config.dlq_stream_key();
    let dlq_length: usize = conn
        .xlen(&dlq_stream)
        .await
        .with_context(|| format!("XLEN on stream {dlq_stream}"))?;

    gauge!(
        "tangle_worker_dlq_length",
        "stream_key" => config.stream_key.clone()
    )
    .set(dlq_length as f64);

    Ok(())
}
