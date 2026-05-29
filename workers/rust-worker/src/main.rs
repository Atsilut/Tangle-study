mod config;
mod consumer;
mod dlq;
mod handlers;
mod job;
mod retry;
mod telemetry;

use anyhow::Context;
use redis::Client;
use tracing::info;

use crate::config::Config;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let config = Config::from_env().context("load worker configuration")?;
    telemetry::init(&config).context("initialize telemetry")?;

    info!(
        stream = %config.full_stream_key(),
        group = %config.consumer_group,
        consumer = %config.consumer_name,
        "starting tangle worker"
    );

    let client = Client::open(config.redis_url.as_str()).context("open redis client")?;
    let mut connection = client
        .get_multiplexed_async_connection()
        .await
        .context("connect to redis")?;
    let pong: String = redis::cmd("PING")
        .query_async(&mut connection)
        .await
        .context("redis PING")?;
    info!(pong = %pong, "redis connected");

    consumer::run(config, client).await
}
