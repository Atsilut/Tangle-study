//! Dead-letter handling for messages that exhaust retries.

use anyhow::Result;
use redis::aio::ConnectionManager;
use tracing::error;

use crate::config::Config;

/// Records a terminal failure. DLQ stream publish arrives in the next milestone.
pub async fn record_exhausted(
    _conn: &mut ConnectionManager,
    config: &Config,
    stream: &str,
    message_id: &str,
    times_delivered: u32,
    err: &anyhow::Error,
) -> Result<()> {
    error!(
        stream = %stream,
        message_id = %message_id,
        times_delivered = times_delivered,
        max_attempts = config.max_attempts,
        dlq_stream = %config.dlq_stream_key(),
        error = %err,
        "retries exhausted; acknowledging message (DLQ publish planned)"
    );
    Ok(())
}
