use anyhow::{bail, Context, Result};
use redis::streams::StreamId;
use redis::Value;

use crate::job::{ChatMessageCreatedJob, MediaUploadedJob};

pub fn decode_chat_message_created(
    expected_type: &str,
    entry: &StreamId,
) -> Result<ChatMessageCreatedJob> {
    decode_job(expected_type, entry)
}

pub fn decode_media_uploaded(expected_type: &str, entry: &StreamId) -> Result<MediaUploadedJob> {
    decode_job(expected_type, entry)
}

fn decode_job<T: serde::de::DeserializeOwned>(expected_type: &str, entry: &StreamId) -> Result<T> {
    let (job_type, payload) = extract_envelope_fields(entry)?;
    if job_type != expected_type {
        bail!(
            "unexpected job type {job_type:?}, expected {expected_type:?} (entry id {})",
            entry.id
        );
    }

    serde_json::from_str(&payload)
        .with_context(|| format!("deserialize payload for entry {}", entry.id))
}

/// Returns `(type, payload)` from a stream entry envelope.
pub fn extract_envelope_fields(entry: &StreamId) -> Result<(String, String)> {
    let job_type = entry
        .map
        .get("type")
        .context("stream entry missing `type` field")?;
    let payload = entry
        .map
        .get("payload")
        .context("stream entry missing `payload` field")?;

    Ok((
        value_to_str(job_type).context("decode `type` field")?.to_owned(),
        value_to_str(payload).context("decode `payload` field")?.to_owned(),
    ))
}

fn value_to_str(value: &Value) -> Result<&str> {
    match value {
        Value::BulkString(bytes) => std::str::from_utf8(bytes).context("field is not valid utf-8"),
        Value::SimpleString(text) => Ok(text.as_str()),
        _ => bail!("expected string field, got {value:?}"),
    }
}

/// Whether a handler error represents a poison-pill stream entry that should be acked, not retried.
pub fn is_malformed_entry(err: &anyhow::Error) -> bool {
    err.chain().any(|cause| {
        let message = cause.to_string();
        message.contains("stream entry missing")
            || message.contains("decode `type` field")
            || message.contains("decode `payload` field")
            || message.contains("expected string field")
            || message.contains("field is not valid utf-8")
            || message.contains("unexpected job type")
            || message.contains("deserialize payload")
    })
}

#[cfg(test)]
mod tests {
    use std::collections::HashMap;

    use super::*;
    use redis::streams::StreamId;

    #[test]
    fn decodes_chat_message_created_payload() {
        let mut map = HashMap::new();
        map.insert(
            "type".to_owned(),
            Value::BulkString(b"chat.message.created".to_vec()),
        );
        map.insert(
            "payload".to_owned(),
            Value::BulkString(
                br#"{"messageId":1,"chatRoomId":2,"senderUserId":3,"body":"hi","sentAt":"2026-01-01T00:00:00+00:00"}"#
                    .to_vec(),
            ),
        );

        let entry = StreamId {
            id: "1-0".to_owned(),
            map,
        };

        let job = decode_chat_message_created("chat.message.created", &entry).unwrap();
        assert_eq!(job.message_id, 1);
        assert_eq!(job.chat_room_id, 2);
        assert_eq!(job.sender_user_id, 3);
        assert_eq!(job.body, "hi");
        assert_eq!(job.sent_at, "2026-01-01T00:00:00+00:00");
    }

    #[test]
    fn malformed_entry_detects_missing_type_field() {
        let entry = StreamId {
            id: "1-0".to_owned(),
            map: HashMap::new(),
        };

        let err = decode_chat_message_created("chat.message.created", &entry).unwrap_err();
        assert!(is_malformed_entry(&err));
    }

    #[test]
    fn malformed_entry_detects_non_string_field_type() {
        let mut map = HashMap::new();
        map.insert("type".to_owned(), Value::Int(1));
        map.insert(
            "payload".to_owned(),
            Value::BulkString(br#"{"messageId":1}"#.to_vec()),
        );

        let entry = StreamId {
            id: "1-0".to_owned(),
            map,
        };

        let err = decode_chat_message_created("chat.message.created", &entry).unwrap_err();
        assert!(is_malformed_entry(&err));
    }

    #[test]
    fn malformed_entry_detects_invalid_json_payload() {
        let mut map = HashMap::new();
        map.insert(
            "type".to_owned(),
            Value::BulkString(b"chat.message.created".to_vec()),
        );
        map.insert(
            "payload".to_owned(),
            Value::BulkString(b"not-json".to_vec()),
        );

        let entry = StreamId {
            id: "1-0".to_owned(),
            map,
        };

        let err = decode_chat_message_created("chat.message.created", &entry).unwrap_err();
        assert!(is_malformed_entry(&err));
    }

    #[test]
    fn decodes_media_uploaded_payload() {
        let mut map = HashMap::new();
        map.insert(
            "type".to_owned(),
            Value::BulkString(b"media.uploaded".to_vec()),
        );
        map.insert(
            "payload".to_owned(),
            Value::BulkString(
                br#"{"mediaAssetId":9,"intendedContext":"Post","kind":"Video","mimeType":"video/mp4","originalObjectKey":"raw/1/a.mp4","originalSizeBytes":500,"targetMaxBytes":2147483648}"#
                    .to_vec(),
            ),
        );

        let entry = StreamId {
            id: "2-0".to_owned(),
            map,
        };

        let job = decode_media_uploaded("media.uploaded", &entry).unwrap();
        assert_eq!(job.media_asset_id, 9);
        assert_eq!(job.intended_context, "Post");
        assert_eq!(job.kind, "Video");
        assert_eq!(job.target_max_bytes, 2_147_483_648);
    }
}
