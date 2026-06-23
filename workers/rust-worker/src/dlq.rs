//! Dead-letter stream publishing and manual replay.

use std::time::{SystemTime, UNIX_EPOCH};

use anyhow::{bail, Context, Result};
use redis::aio::ConnectionManager;
use redis::streams::{StreamId, StreamRangeReply};
use redis::{AsyncCommands, Value};
use tracing::{info, warn};

use crate::config::Config;
use crate::message;

const FIELD_SOURCE_STREAM: &str = "source_stream";
const FIELD_SOURCE_MESSAGE_ID: &str = "source_message_id";
const FIELD_TYPE: &str = "type";
const FIELD_PAYLOAD: &str = "payload";
const FIELD_ERROR: &str = "error";
const FIELD_TIMES_DELIVERED: &str = "times_delivered";
const FIELD_FAILED_AT: &str = "failed_at";

/// Writes a terminal failure record to the DLQ stream, then logs the outcome.
pub async fn publish_exhausted(
    conn: &mut ConnectionManager,
    config: &Config,
    source_stream: &str,
    source_entry: Option<&StreamId>,
    source_message_id: &str,
    times_delivered: u32,
    err: &anyhow::Error,
) -> Result<()> {
    let (job_type, payload) = match source_entry {
        Some(entry) => message::extract_envelope_fields(entry).with_context(|| {
            format!("extract envelope for exhausted message {source_message_id}")
        })?,
        None => {
            warn!(
                source_stream = %source_stream,
                source_message_id = %source_message_id,
                "source entry body unavailable; DLQ record will omit type/payload"
            );
            (String::new(), String::new())
        }
    };

    let dlq_id = publish_record(
        conn,
        config,
        source_stream,
        source_message_id,
        &job_type,
        &payload,
        times_delivered,
        err,
    )
    .await?;

    info!(
        source_stream = %source_stream,
        source_message_id = %source_message_id,
        dlq_stream = %config.dlq_stream_key(),
        dlq_message_id = %dlq_id,
        times_delivered = times_delivered,
        error = %err,
        "published exhausted message to DLQ"
    );

    Ok(())
}

async fn publish_record(
    conn: &mut ConnectionManager,
    config: &Config,
    source_stream: &str,
    source_message_id: &str,
    job_type: &str,
    payload: &str,
    times_delivered: u32,
    err: &anyhow::Error,
) -> Result<String> {
    let dlq_stream = config.dlq_stream_key();
    let failed_at = unix_ms_now().to_string();

    let dlq_id: String = conn
        .xadd(
            &dlq_stream,
            "*",
            &[
                (FIELD_SOURCE_STREAM, source_stream),
                (FIELD_SOURCE_MESSAGE_ID, source_message_id),
                (FIELD_TYPE, job_type),
                (FIELD_PAYLOAD, payload),
                (FIELD_ERROR, &err.to_string()),
                (FIELD_TIMES_DELIVERED, &times_delivered.to_string()),
                (FIELD_FAILED_AT, &failed_at),
            ],
        )
        .await
        .with_context(|| format!("XADD exhausted record to DLQ stream {dlq_stream}"))?;

    Ok(dlq_id)
}

pub async fn fetch_stream_entry(
    conn: &mut ConnectionManager,
    stream: &str,
    message_id: &str,
) -> Result<Option<StreamId>> {
    let reply: StreamRangeReply = conn
        .xrange(stream, message_id, message_id)
        .await
        .with_context(|| format!("XRANGE {message_id} on stream {stream}"))?;

    Ok(reply.ids.into_iter().next())
}

/// Re-enqueues up to `count` entries from the DLQ back onto the main work stream.
pub async fn run_replay(conn: &mut ConnectionManager, config: &Config) -> Result<()> {
    let dlq_stream = config.dlq_stream_key();
    let target_stream = config.full_stream_key();
    let count = config.replay_count;
    let dry_run = config.replay_dry_run;
    let delete_after = config.replay_delete;

    info!(
        dlq_stream = %dlq_stream,
        target_stream = %target_stream,
        count = count,
        dry_run = dry_run,
        delete_after = delete_after,
        "starting DLQ replay"
    );

    let entries: StreamRangeReply = conn
        .xrange_count(&dlq_stream, "-", "+", count)
        .await
        .with_context(|| format!("XRANGE DLQ stream {dlq_stream}"))?;

    if entries.ids.is_empty() {
        info!(dlq_stream = %dlq_stream, "DLQ is empty; nothing to replay");
        return Ok(());
    }

    let mut replayed = 0usize;
    for entry in entries.ids {
        let dlq_id = entry.id.clone();
        let (job_type, payload) = parse_dlq_entry(&entry)?;

        if dry_run {
            info!(
                dlq_message_id = %dlq_id,
                job_type = %job_type,
                "dry run: would replay DLQ entry"
            );
            replayed += 1;
            continue;
        }

        let new_id: String = conn
            .xadd(
                &target_stream,
                "*",
                &[(FIELD_TYPE, job_type.as_str()), (FIELD_PAYLOAD, payload.as_str())],
            )
            .await
            .with_context(|| format!("re-enqueue replayed job to {target_stream}"))?;

        if delete_after {
            let deleted: usize = conn
                .xdel(&dlq_stream, &[dlq_id.as_str()])
                .await
                .with_context(|| format!("XDEL {dlq_id} from DLQ stream {dlq_stream}"))?;
            if deleted == 0 {
                warn!(
                    dlq_message_id = %dlq_id,
                    "XDEL returned 0 after replay"
                );
            }
        }

        info!(
            dlq_message_id = %dlq_id,
            target_stream = %target_stream,
            new_message_id = %new_id,
            job_type = %job_type,
            "replayed DLQ entry to work stream"
        );
        replayed += 1;
    }

    info!(
        dlq_stream = %dlq_stream,
        replayed = replayed,
        dry_run = dry_run,
        "DLQ replay finished"
    );

    Ok(())
}

fn parse_dlq_entry(entry: &StreamId) -> Result<(String, String)> {
    let (job_type, payload) = message::extract_envelope_fields(entry)
        .with_context(|| format!("parse DLQ entry {}", entry.id))?;

    if job_type.trim().is_empty() {
        bail!("DLQ entry {} has empty `type`; cannot replay", entry.id);
    }
    if payload.is_empty() {
        bail!("DLQ entry {} has empty `payload`; cannot replay", entry.id);
    }

    Ok((job_type.trim().to_owned(), payload))
}

fn unix_ms_now() -> u128 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_millis())
        .unwrap_or(0)
}

#[cfg(test)]
mod tests {
    use std::collections::HashMap;

    use super::*;
    use redis::streams::StreamId;

    #[test]
    fn parse_dlq_entry_extracts_type_and_payload() {
        let mut map = HashMap::new();
        map.insert(
            FIELD_TYPE.to_owned(),
            Value::BulkString(b"chat.message.created".to_vec()),
        );
        map.insert(
            FIELD_PAYLOAD.to_owned(),
            Value::BulkString(br#"{"messageId":1}"#.to_vec()),
        );

        let entry = StreamId {
            id: "9-0".to_owned(),
            map,
        };

        let (job_type, payload) = parse_dlq_entry(&entry).unwrap();
        assert_eq!(job_type, "chat.message.created");
        assert_eq!(payload, r#"{"messageId":1}"#);
    }

    #[test]
    fn parse_dlq_entry_rejects_empty_payload() {
        let mut map = HashMap::new();
        map.insert(
            FIELD_TYPE.to_owned(),
            Value::BulkString(b"chat.message.created".to_vec()),
        );
        map.insert(
            FIELD_PAYLOAD.to_owned(),
            Value::BulkString(b"".to_vec()),
        );

        let entry = StreamId {
            id: "9-0".to_owned(),
            map,
        };

        assert!(parse_dlq_entry(&entry).is_err());
    }
}
