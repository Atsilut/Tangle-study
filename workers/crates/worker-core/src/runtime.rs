use anyhow::Context;
use redis::aio::ConnectionManager;
use redis::Client;
use tracing::info;

use crate::config::Config;
use crate::consumer;
use crate::dlq;
use crate::handler::StreamHandler;
use crate::metrics;
use crate::telemetry;

pub fn is_replay_mode() -> bool {
    std::env::args().nth(1).as_deref() == Some("replay")
}

pub async fn bootstrap(worker_name: &str, config: Config, handler: &dyn StreamHandler) -> anyhow::Result<()> {
    let replay_mode = is_replay_mode();

    telemetry::init(&config).context("initialize telemetry")?;

    let client = Client::open(config.stream.redis_url.as_str()).context("open redis client")?;
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
            "starting {worker_name} in replay mode"
        );
        dlq::run_replay(&mut connection, &config).await
    } else {
        metrics::init(config.stream.metrics_port).context("initialize Prometheus metrics")?;
        info!(
            stream = %config.full_stream_key(),
            group = %config.stream.consumer_group,
            consumer = %config.stream.consumer_name,
            metrics_port = config.stream.metrics_port,
            "starting {worker_name}"
        );

        consumer::run(config, connection, handler).await
    }
}
