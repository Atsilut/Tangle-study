mod cluster;
mod handlers;

use anyhow::Context;
use redis::aio::ConnectionManager;
use redis::Client;
use tracing::info;
use worker_core::{consumer, dlq, metrics, telemetry, Config};

use crate::handlers::ChatLocationHandler;

const ALLOWED_STREAM_KEYS: &[&str] = &["chat.message.created", "location.cluster"];

fn validate_config(config: &Config, consumer: bool) -> anyhow::Result<()> {
    config.validate_stream_key(ALLOWED_STREAM_KEYS)?;
    if consumer && config.stream_key == "location.cluster" {
        config.validate_callback_env()?;
    }
    Ok(())
}

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let config = Config::from_env().context("load worker configuration")?;
    let replay_mode = std::env::args().nth(1).as_deref() == Some("replay");
    validate_config(&config, !replay_mode).context("validate worker configuration")?;
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

    if replay_mode {
        info!(
            dlq_stream = %config.dlq_stream_key(),
            target_stream = %config.full_stream_key(),
            "starting tangle worker in replay mode"
        );
        dlq::run_replay(&mut connection, &config).await
    } else {
        metrics::init(config.metrics_port).context("initialize Prometheus metrics")?;
        info!(
            stream = %config.full_stream_key(),
            group = %config.consumer_group,
            consumer = %config.consumer_name,
            metrics_port = config.metrics_port,
            "starting tangle worker"
        );

        let handler = ChatLocationHandler;
        consumer::run(config, connection, &handler).await
    }
}
