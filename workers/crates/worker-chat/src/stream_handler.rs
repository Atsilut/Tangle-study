use anyhow::Result;
use async_trait::async_trait;
use redis::streams::StreamId;
use reqwest::Client;
use worker_core::handler::StreamHandler;
use worker_core::Config;

use crate::handler;
use crate::message;

pub struct ChatStreamHandler;

#[async_trait]
impl StreamHandler for ChatStreamHandler {
    fn needs_http_client(&self) -> bool {
        false
    }

    async fn dispatch(
        &self,
        _config: &Config,
        entry: &StreamId,
        _http: Option<&Client>,
    ) -> Result<()> {
        let job = message::decode_chat_message_created(entry)?;
        handler::handle(&job).await
    }
}
