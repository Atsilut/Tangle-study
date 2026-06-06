mod config;
mod consumer;
mod dlq;
mod handlers;
mod job;
mod message;
mod retry;
mod telemetry;

use anyhow::Context;
use redis::aio::ConnectionManager;
use redis::Client;
use tracing::info;

use crate::config::Config;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let config = Config::from_env().context("load worker configuration")?;
    telemetry::init(&config).context("initialize telemetry")?;

    let client = Client::open(config.redis_url.as_str()).context("open redis client")?;
    let mut connection = ConnectionManager::new(client)
        .await
        .context("connect to redis")?;
    let pong: String = redis::cmd("PING")
        .query_async(&mut connection)
        .await
        .context("redis PING")?;
    info!(pong = %pong, "redis connected");

    if std::env::args().nth(1).as_deref() == Some("replay") {
        info!(
            dlq_stream = %config.dlq_stream_key(),
            target_stream = %config.full_stream_key(),
            "starting tangle worker in replay mode"
        );
        dlq::run_replay(&mut connection, &config).await
    } else {
        info!(
            stream = %config.full_stream_key(),
            group = %config.consumer_group,
            consumer = %config.consumer_name,
            "starting tangle worker"
        );
        consumer::run(config, connection).await
    }
}
