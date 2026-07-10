use anyhow::{Context, Result};
use redis::aio::ConnectionManager;
use redis::streams::{StreamClaimReply, StreamPendingCountReply, StreamReadOptions, StreamReadReply};
use redis::{AsyncCommands, Client, RedisError};
use tracing::{error, info, warn};

use crate::config::Config;
use crate::dlq;
use crate::error;
use crate::handler::StreamHandler;
use crate::http_client;
use crate::metrics::{self, JobOutcome};
use crate::retry;

/// Ensures the consumer group exists (idempotent). New groups read from the start of the stream (`0`).
pub async fn ensure_consumer_group(conn: &mut ConnectionManager, config: &Config) -> Result<()> {
    let stream = config.full_stream_key();
    let group = &config.stream.consumer_group;

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
pub async fn run(
    config: Config,
    client: Client,
    handler: &dyn StreamHandler,
) -> Result<()> {
    let mut read_conn = ConnectionManager::new(client.clone())
        .await
        .context("connect redis read client")?;
    let mut admin_conn = ConnectionManager::new(client)
        .await
        .context("connect redis admin client")?;
    ensure_consumer_group(&mut admin_conn, &config).await?;

    let http = if handler.needs_http_client() {
        Some(http_client::build_callback_client(&config.callback)?)
    } else {
        None
    };

    let stream = config.full_stream_key();
    info!(
        stream = %stream,
        dlq_stream = %config.dlq_stream_key(),
        group = %config.stream.consumer_group,
        consumer = %config.stream.consumer_name,
        block_ms = config.stream.block_ms,
        batch_count = config.stream.batch_count,
        max_attempts = config.stream.max_attempts,
        retry_base_ms = config.stream.retry_base_ms,
        retry_max_ms = config.stream.retry_max_ms,
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
            res = read_and_process_batch(&mut read_conn, &mut admin_conn, &config, http.as_ref(), handler) => {
                res.context("process stream batch")?;
            }
        }
    }

    Ok(())
}

async fn read_and_process_batch(
    read_conn: &mut ConnectionManager,
    admin_conn: &mut ConnectionManager,
    config: &Config,
    http: Option<&reqwest::Client>,
    handler: &dyn StreamHandler,
) -> Result<()> {
    // Reclaim before reading new work so a previously hung delivery can recover
    // even when the next XREADGROUP also returns quickly with more jobs.
    reclaim_pending_retries(admin_conn, config, http, handler).await?;
    read_new_messages(read_conn, admin_conn, config, http, handler).await?;
    metrics::refresh_queue_gauges(admin_conn, config).await?;
    Ok(())
}

async fn read_new_messages(
    read_conn: &mut ConnectionManager,
    admin_conn: &mut ConnectionManager,
    config: &Config,
    http: Option<&reqwest::Client>,
    handler: &dyn StreamHandler,
) -> Result<()> {
    let stream = config.full_stream_key();
    let opts = StreamReadOptions::default()
        .group(&config.stream.consumer_group, &config.stream.consumer_name)
        .count(config.stream.batch_count)
        .block(config.stream.block_ms as usize);

    let reply: StreamReadReply = match read_conn.xread_options(&[&stream], &[">"], &opts).await {
        Ok(reply) => reply,
        Err(err) if error::is_transient_redis_error(&err) => return Ok(()),
        Err(err) => {
            return Err(err).with_context(|| format!("XREADGROUP on stream {stream}"));
        }
    };

    let mut work_items = Vec::new();
    for key in reply.keys {
        for entry in key.ids {
            work_items.push((key.key.clone(), entry, 1));
        }
    }

    for (stream_key, entry, times_delivered) in work_items {
        let dispatch_result = handler.dispatch(config, &entry, http).await;
        finalize_entry(
            admin_conn,
            config,
            &stream_key,
            &entry,
            times_delivered,
            dispatch_result,
        )
        .await?;
    }

    Ok(())
}

async fn reclaim_pending_retries(
    admin_conn: &mut ConnectionManager,
    config: &Config,
    http: Option<&reqwest::Client>,
    handler: &dyn StreamHandler,
) -> Result<()> {
    let stream = config.full_stream_key();
    let pending: StreamPendingCountReply = match admin_conn
        .xpending_count(
            &stream,
            &config.stream.consumer_group,
            "-",
            "+",
            config.stream.batch_count,
        )
        .await
    {
        Ok(pending) => pending,
        Err(err) if error::is_transient_redis_error(&err) => return Ok(()),
        Err(err) => {
            return Err(err).with_context(|| format!("XPENDING on stream {stream}"));
        }
    };

    for item in pending.ids {
        let times_delivered = u32::try_from(item.times_delivered)
            .context("pending entry times_delivered exceeds u32")?;

        if retry::is_terminal(times_delivered, config.stream.max_attempts) {
            let err = anyhow::anyhow!("max delivery attempts ({times_delivered}) reached");
            let source_entry = dlq::fetch_stream_entry(admin_conn, &stream, &item.id).await?;
            dlq::publish_exhausted(
                admin_conn,
                config,
                &stream,
                source_entry.as_ref(),
                &item.id,
                times_delivered,
                &err,
            )
            .await?;
            ack(admin_conn, &stream, &config.stream.consumer_group, &item.id).await?;
            metrics::record_job_processed(&config.stream.stream_key, JobOutcome::Dlq);
            continue;
        }

        if !retry::eligible_for_retry(
            item.last_delivered_ms as u64,
            times_delivered,
            config.stream.max_attempts,
            config.stream.retry_base_ms,
            config.stream.retry_max_ms,
            config.stream.retry_jitter_pct,
            &item.id,
        ) {
            continue;
        }

        let min_idle = retry::backoff_delay_ms(
            times_delivered,
            config.stream.retry_base_ms,
            config.stream.retry_max_ms,
            config.stream.retry_jitter_pct,
            &item.id,
        ) as usize;

        let claim_reply: StreamClaimReply = admin_conn
            .xclaim(
                &stream,
                &config.stream.consumer_group,
                &config.stream.consumer_name,
                min_idle,
                &[item.id.as_str()],
            )
            .await
            .with_context(|| format!("XCLAIM {} on stream {stream}", item.id))?;

        for entry in claim_reply.ids {
            let delivery_count = times_delivered.saturating_add(1);
            info!(
                stream = %stream,
                message_id = %entry.id,
                times_delivered = delivery_count,
                min_idle_ms = min_idle,
                "reclaimed pending message for retry"
            );
            let dispatch_result = handler.dispatch(config, &entry, http).await;
            finalize_entry(
                admin_conn,
                config,
                &stream,
                &entry,
                delivery_count,
                dispatch_result,
            )
            .await?;
        }
    }

    Ok(())
}

async fn finalize_entry(
    conn: &mut ConnectionManager,
    config: &Config,
    stream: &str,
    entry: &redis::streams::StreamId,
    times_delivered: u32,
    dispatch_result: Result<()>,
) -> Result<()> {
    let message_id = entry.id.as_str();

    match dispatch_result {
        Ok(()) => {
            ack(conn, stream, &config.stream.consumer_group, message_id).await?;
            metrics::record_job_processed(&config.stream.stream_key, JobOutcome::Success);
            info!(
                stream = %stream,
                message_id = %message_id,
                stream_key = %config.stream.stream_key,
                times_delivered = times_delivered,
                "processed and acked message"
            );
        }
        Err(err) => {
            if error::is_malformed(&err) {
                warn!(
                    stream = %stream,
                    message_id = %message_id,
                    error = %err,
                    "skipping malformed message; acking to avoid poison-pill loop"
                );
                ack(conn, stream, &config.stream.consumer_group, message_id).await?;
                metrics::record_job_processed(&config.stream.stream_key, JobOutcome::Malformed);
                return Ok(());
            }

            if retry::is_terminal(times_delivered, config.stream.max_attempts) {
                dlq::publish_exhausted(
                    conn,
                    config,
                    stream,
                    Some(entry),
                    message_id,
                    times_delivered,
                    &err,
                )
                .await?;
                ack(conn, stream, &config.stream.consumer_group, message_id).await?;
                metrics::record_job_processed(&config.stream.stream_key, JobOutcome::Dlq);
            } else {
                metrics::record_job_processed(&config.stream.stream_key, JobOutcome::Failure);
                let retry_after_ms = retry::backoff_delay_ms(
                    times_delivered,
                    config.stream.retry_base_ms,
                    config.stream.retry_max_ms,
                    config.stream.retry_jitter_pct,
                    message_id,
                );
                error!(
                    stream = %stream,
                    message_id = %message_id,
                    times_delivered = times_delivered,
                    retry_after_ms = retry_after_ms,
                    error = %err,
                    "handler failed; message left in pending entries list for retry"
                );
            }
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
