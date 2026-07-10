use std::time::Duration;

use anyhow::{bail, Context, Result};
use async_trait::async_trait;
use redis::streams::StreamId;
use reqwest::Client;
use worker_core::handler::StreamHandler;
use worker_core::Config;

use crate::config::MediaConfig;
use crate::handler;
use crate::message;

/// Hard ceiling for one media job so a wedged Azure/ffmpeg call cannot stall the
/// consumer loop (and reclaim) indefinitely. Must stay under harness poll timeouts.
const JOB_TIMEOUT: Duration = Duration::from_secs(45);

pub struct MediaStreamHandler {
    pub media: MediaConfig,
}

#[async_trait]
impl StreamHandler for MediaStreamHandler {
    async fn dispatch(
        &self,
        _config: &Config,
        entry: &StreamId,
        http: Option<&Client>,
    ) -> Result<()> {
        let job = message::decode_media_uploaded(entry)?;
        let http = http.context("media handler requires HTTP client")?;
        match tokio::time::timeout(JOB_TIMEOUT, handler::handle(&job, &self.media, http)).await {
            Ok(result) => result,
            Err(_) => bail!(
                "media.uploaded job timed out after {}s (media_asset_id={})",
                JOB_TIMEOUT.as_secs(),
                job.media_asset_id
            ),
        }
    }
}
