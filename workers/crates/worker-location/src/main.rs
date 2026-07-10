mod cluster;
mod config;
mod handler;
mod job;
mod message;
mod stream_handler;

use anyhow::Context;
use worker_core::runtime;

use crate::config::LocationConfig;
use crate::stream_handler::LocationStreamHandler;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let location_config = LocationConfig::from_env().context("load worker configuration")?;

    if runtime::is_replay_mode() {
        location_config
            .validate_replay()
            .context("validate worker configuration")?;
    } else {
        location_config
            .validate_consumer()
            .context("validate worker configuration")?;
    }

    runtime::bootstrap("worker-location", location_config.core, &LocationStreamHandler).await
}
