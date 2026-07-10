use std::time::Duration;

use anyhow::Context;
use reqwest::Client;

use crate::config::CallbackConfig;

pub fn build_callback_client(config: &CallbackConfig) -> anyhow::Result<Client> {
    Client::builder()
        .connect_timeout(Duration::from_millis(config.connect_timeout_ms))
        .timeout(Duration::from_millis(config.timeout_ms))
        .build()
        .context("build callback HTTP client")
}
