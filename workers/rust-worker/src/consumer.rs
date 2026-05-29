use anyhow::Result;
use redis::Client;
use tracing::info;

use crate::config::Config;

/// Consumer loop entry point. Scaffold waits for shutdown; `XREADGROUP` arrives in todo 2.
pub async fn run(config: Config, _client: Client) -> Result<()> {
    info!(
        stream = %config.full_stream_key(),
        dlq_stream = %config.dlq_stream_key(),
        group = %config.consumer_group,
        consumer = %config.consumer_name,
        block_ms = config.block_ms,
        batch_count = config.batch_count,
        max_attempts = config.max_attempts,
        "worker scaffold ready (stream consumer loop not yet implemented)"
    );

    tokio::signal::ctrl_c().await?;
    info!("shutdown signal received");
    Ok(())
}
