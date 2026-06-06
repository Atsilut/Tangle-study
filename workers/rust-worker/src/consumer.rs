use anyhow::{Context, Result};
use redis::aio::ConnectionManager;
use redis::streams::{StreamReadOptions, StreamReadReply};
use redis::{AsyncCommands, RedisError};
use tracing::{error, info, warn};

use crate::config::Config;
use crate::handlers;
use crate::message;

/// Ensures the consumer group exists (idempotent). New groups read from the start of the stream (`0`).
pub async fn ensure_consumer_group(conn: &mut ConnectionManager, config: &Config) -> Result<()> {
    let stream = config.full_stream_key();
    let group = &config.consumer_group;

    match conn
        .xgroup_create_mkstream::<_, _, _, ()>(&stream, group, "0")
        .await
    {
        Ok(()) => {
            info!(stream = %stream, group = %group, "created consumer group");
            Ok(())
        }
        Err(err) if is_busygroup(&err) => {
            info!(stream = %stream, group = %group, "consumer group already exists");
            Ok(())
        }
        Err(err) => Err(err).with_context(|| {
            format!("create consumer group {group} on stream {stream}")
        }),
    }
}

/// Blocking `XREADGROUP` loop; acks entries after successful handler execution.
pub async fn run(config: Config, mut conn: ConnectionManager) -> Result<()> {
    ensure_consumer_group(&mut conn, &config).await?;

    let stream = config.full_stream_key();
    info!(
        stream = %stream,
        dlq_stream = %config.dlq_stream_key(),
        group = %config.consumer_group,
        consumer = %config.consumer_name,
        block_ms = config.block_ms,
        batch_count = config.batch_count,
        "consumer loop started"
    );

    let mut shutdown = std::pin::pin!(tokio::signal::ctrl_c());

    loop {
        tokio::select! {
            res = shutdown.as_mut() => {
                res.context("wait for shutdown signal")?;
                info!("shutdown signal received");
                break;
            }
            res = read_and_process_batch(&mut conn, &config) => {
                res.context("process stream batch")?;
            }
        }
    }

    Ok(())
}

async fn read_and_process_batch(conn: &mut ConnectionManager, config: &Config) -> Result<()> {
    let stream = config.full_stream_key();
    let opts = StreamReadOptions::default()
        .group(&config.consumer_group, &config.consumer_name)
        .count(config.batch_count)
        .block(config.block_ms as usize);

    let reply: StreamReadReply = conn
        .xread_options(&[&stream], &[">"], &opts)
        .await
        .with_context(|| format!("XREADGROUP on stream {stream}"))?;

    for key in reply.keys {
        for entry in key.ids {
            process_entry(conn, config, &key.key, &entry).await?;
        }
    }

    Ok(())
}

async fn process_entry(
    conn: &mut ConnectionManager,
    config: &Config,
    stream: &str,
    entry: &redis::streams::StreamId,
) -> Result<()> {
    let message_id = entry.id.as_str();

    match message::decode_chat_message_created(&config.stream_key, entry) {
        Ok(job) => match handlers::dispatch_chat_message_created(&job).await {
            Ok(()) => {
                ack(conn, stream, &config.consumer_group, message_id).await?;
                info!(
                    stream = %stream,
                    message_id = %message_id,
                    chat_message_id = job.message_id,
                    "processed and acked message"
                );
            }
            Err(err) => {
                error!(
                    stream = %stream,
                    message_id = %message_id,
                    error = %err,
                    "handler failed; message left in pending entries list for retry"
                );
            }
        },
        Err(err) => {
            warn!(
                stream = %stream,
                message_id = %message_id,
                error = %err,
                "skipping malformed message; acking to avoid poison-pill loop"
            );
            ack(conn, stream, &config.consumer_group, message_id).await?;
        }
    }

    Ok(())
}

async fn ack(
    conn: &mut ConnectionManager,
    stream: &str,
    group: &str,
    message_id: &str,
) -> Result<()> {
    let acked: usize = conn
        .xack(stream, group, &[message_id])
        .await
        .with_context(|| format!("XACK {message_id} on stream {stream}"))?;

    if acked == 0 {
        warn!(
            stream = %stream,
            group = %group,
            message_id = %message_id,
            "XACK returned 0 (message may already be acknowledged)"
        );
    }

    Ok(())
}

fn is_busygroup(err: &RedisError) -> bool {
    err.code() == Some("BUSYGROUP")
}
