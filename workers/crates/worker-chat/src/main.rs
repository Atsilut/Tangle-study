mod config;
mod handler;
mod job;
mod message;
mod stream_handler;

use anyhow::Context;
use worker_core::runtime;

use crate::config::ChatConfig;
use crate::stream_handler::ChatStreamHandler;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let chat_config = ChatConfig::from_env().context("load worker configuration")?;

    if runtime::is_replay_mode() {
        chat_config.validate_replay().context("validate worker configuration")?;
    } else {
        chat_config
            .validate_consumer()
            .context("validate worker configuration")?;
    }

    runtime::bootstrap("worker-chat", chat_config.core, &ChatStreamHandler).await
}
