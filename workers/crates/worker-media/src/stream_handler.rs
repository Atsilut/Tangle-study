use anyhow::Result;
use async_trait::async_trait;
use redis::streams::StreamId;
use reqwest::Client;
use worker_core::handler::StreamHandler;
use worker_core::Config;

use crate::config::MediaConfig;
use crate::handler;
use crate::message;

pub struct MediaStreamHandler {
    pub media: MediaConfig,
}

#[async_trait]
impl StreamHandler for MediaStreamHandler {
    async fn dispatch(
        &self,
        _config: &Config,
        entry: &StreamId,
        http: &Client,
    ) -> Result<()> {
        let job = message::decode_media_uploaded(entry)?;
        handler::handle(&job, &self.media, http).await
    }
}
