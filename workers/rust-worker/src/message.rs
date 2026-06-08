use anyhow::{bail, Context, Result};
use redis::streams::StreamId;
use redis::Value;

use crate::job::ChatMessageCreatedJob;

pub fn decode_chat_message_created(
    expected_type: &str,
    entry: &StreamId,
) -> Result<ChatMessageCreatedJob> {
    let job_type = entry
        .map
        .get("type")
        .context("stream entry missing `type` field")?;
    let payload = entry
        .map
        .get("payload")
        .context("stream entry missing `payload` field")?;

    let job_type = value_to_str(job_type).context("decode `type` field")?;
    let payload = value_to_str(payload).context("decode `payload` field")?;

    if job_type != expected_type {
        bail!(
            "unexpected job type {job_type:?}, expected {expected_type:?} (entry id {})",
            entry.id
        );
    }

    serde_json::from_str(payload)
        .with_context(|| format!("deserialize payload for entry {}", entry.id))
}

fn value_to_str(value: &Value) -> Result<&str> {
    match value {
        Value::BulkString(bytes) => std::str::from_utf8(bytes).context("field is not valid utf-8"),
        Value::SimpleString(text) => Ok(text.as_str()),
        _ => bail!("expected string field, got {value:?}"),
    }
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
        value_to_str(job_type)?.to_owned(),
        value_to_str(payload)?.to_owned(),
    ))
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
}
