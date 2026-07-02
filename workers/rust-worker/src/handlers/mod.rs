pub mod chat_message_created;
pub mod location_cluster;

use anyhow::{bail, Result};
use async_trait::async_trait;
use redis::streams::StreamId;
use reqwest::Client;
use worker_core::handler::StreamHandler;
use worker_core::message;
use worker_core::Config;

pub struct ChatLocationHandler;

#[async_trait]
impl StreamHandler for ChatLocationHandler {
    async fn dispatch(
        &self,
        config: &Config,
        entry: &StreamId,
        http: &Client,
    ) -> Result<()> {
        match config.stream_key.as_str() {
            "chat.message.created" => {
                let job = message::decode_chat_message_created(&config.stream_key, entry)?;
                chat_message_created::handle(&job).await
            }
            "location.cluster" => {
                let job = message::decode_location_cluster(&config.stream_key, entry)?;
                location_cluster::handle(&job, config, http).await
            }
            other => bail!("unsupported worker stream key {other}"),
        }
    }
}
