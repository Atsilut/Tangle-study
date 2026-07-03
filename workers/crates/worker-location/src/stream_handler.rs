use anyhow::{Context, Result};
use async_trait::async_trait;
use redis::streams::StreamId;
use reqwest::Client;
use worker_core::handler::StreamHandler;
use worker_core::Config;

use crate::handler;
use crate::message;

pub struct LocationStreamHandler;

#[async_trait]
impl StreamHandler for LocationStreamHandler {
    async fn dispatch(
        &self,
        config: &Config,
        entry: &StreamId,
        http: Option<&Client>,
    ) -> Result<()> {
        let job = message::decode_location_cluster(entry)?;
        let http = http.context("location handler requires HTTP client")?;
        handler::handle(&job, config, http).await
    }
}
