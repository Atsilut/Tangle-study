use anyhow::Result;
use async_trait::async_trait;
use redis::streams::StreamId;
use reqwest::Client;

use crate::config::Config;

#[async_trait]
pub trait StreamHandler: Send + Sync {
    fn needs_http_client(&self) -> bool {
        true
    }

    async fn dispatch(
        &self,
        config: &Config,
        entry: &StreamId,
        http: Option<&Client>,
    ) -> Result<()>;
}
