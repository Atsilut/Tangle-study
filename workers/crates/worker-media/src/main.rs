mod api_callback;
mod config;
mod encode_plan;
mod handler;
mod message;
mod processing;
mod probe;
mod storage;
mod stream_handler;

use anyhow::Context;
use redis::aio::ConnectionManager;
use redis::Client;
use tracing::info;
use worker_core::{consumer, dlq, metrics, telemetry};

use crate::config::MediaConfig;
use crate::stream_handler::MediaStreamHandler;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let media_config = MediaConfig::from_env().context("load worker configuration")?;
    let replay_mode = std::env::args().nth(1).as_deref() == Some("replay");

    if replay_mode {
        media_config.validate_replay().context("validate worker configuration")?;
    } else {
        media_config
            .validate_consumer()
            .context("validate worker configuration")?;
    }

    telemetry::init(&media_config.core).context("initialize telemetry")?;

    let client = Client::open(media_config.core.redis_url.as_str()).context("open redis client")?;
    let mut connection = ConnectionManager::new(client)
        .await
        .context("connect to redis")?;
    let pong: String = redis::cmd("PING")
        .query_async(&mut connection)
        .await
        .context("redis PING")?;
    info!(pong = %pong, "redis connected");

    let core = media_config.core.clone();

    if replay_mode {
        info!(
            dlq_stream = %core.dlq_stream_key(),
            target_stream = %core.full_stream_key(),
            "starting worker-media in replay mode"
        );
        dlq::run_replay(&mut connection, &core).await
    } else {
        metrics::init(core.metrics_port).context("initialize Prometheus metrics")?;
        info!(
            stream = %core.full_stream_key(),
            group = %core.consumer_group,
            consumer = %core.consumer_name,
            metrics_port = core.metrics_port,
            "starting worker-media"
        );

        let stream_handler = MediaStreamHandler {
            media: media_config,
        };
        consumer::run(core, connection, &stream_handler).await
    }
}
