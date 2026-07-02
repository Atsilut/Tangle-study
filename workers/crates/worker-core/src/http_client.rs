use std::time::Duration;

use anyhow::Context;
use reqwest::Client;

use crate::config::Config;

pub fn build_callback_client(config: &Config) -> anyhow::Result<Client> {
    Client::builder()
        .connect_timeout(Duration::from_millis(config.callback_connect_timeout_ms))
        .timeout(Duration::from_millis(config.callback_timeout_ms))
        .build()
        .context("build callback HTTP client")
}
