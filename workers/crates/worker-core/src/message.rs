use redis::streams::StreamId;
use redis::Value;
use serde::de::DeserializeOwned;

use anyhow::{bail, Context, Result};

use crate::error::wrap_malformed;

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

/// Decodes a typed job payload from a stream entry envelope.
pub fn decode_typed_job<T: DeserializeOwned>(expected_type: &str, entry: &StreamId) -> Result<T> {
    let (job_type, payload) = extract_envelope_fields(entry).map_err(wrap_malformed)?;
    if job_type != expected_type {
        return Err(wrap_malformed(anyhow::anyhow!(
            "unexpected job type {job_type:?}, expected {expected_type:?} (entry id {})",
            entry.id
        )));
    }

    serde_json::from_str(&payload)
        .with_context(|| format!("deserialize payload for entry {}", entry.id))
        .map_err(wrap_malformed)
}

fn value_to_str(value: &Value) -> Result<&str> {
    match value {
        Value::BulkString(bytes) => std::str::from_utf8(bytes).context("field is not valid utf-8"),
        Value::SimpleString(text) => Ok(text.as_str()),
        _ => bail!("expected string field, got {value:?}"),
    }
}

#[cfg(test)]
mod tests {
    use std::collections::HashMap;

    use super::*;
    use crate::error::is_malformed;
    use crate::test_support::stream_id;
    use redis::Value;
    use serde::Deserialize;

    #[derive(Debug, Deserialize)]
    #[serde(rename_all = "camelCase")]
    struct SampleJob {
        message_id: i64,
    }

    #[test]
    fn decode_typed_job_parses_payload() {
        let mut map = HashMap::new();
        map.insert(
            "type".to_owned(),
            Value::BulkString(b"chat.message.created".to_vec()),
        );
        map.insert(
            "payload".to_owned(),
            Value::BulkString(br#"{"messageId":1}"#.to_vec()),
        );

        let entry = stream_id("1-0", map);

        let job: SampleJob = decode_typed_job("chat.message.created", &entry).unwrap();
        assert_eq!(job.message_id, 1);
    }

    #[test]
    fn decode_typed_job_marks_wrong_type_as_malformed() {
        let mut map = HashMap::new();
        map.insert(
            "type".to_owned(),
            Value::BulkString(b"other.type".to_vec()),
        );
        map.insert(
            "payload".to_owned(),
            Value::BulkString(br#"{"messageId":1}"#.to_vec()),
        );

        let entry = stream_id("1-0", map);

        let err = decode_typed_job::<SampleJob>("chat.message.created", &entry).unwrap_err();
        assert!(is_malformed(&err));
    }

    #[test]
    fn decode_typed_job_marks_missing_type_as_malformed() {
        let entry = stream_id("1-0", HashMap::new());

        let err = decode_typed_job::<SampleJob>("chat.message.created", &entry).unwrap_err();
        assert!(is_malformed(&err));
    }

    #[test]
    fn decode_typed_job_marks_invalid_json_as_malformed() {
        let mut map = HashMap::new();
        map.insert(
            "type".to_owned(),
            Value::BulkString(b"chat.message.created".to_vec()),
        );
        map.insert(
            "payload".to_owned(),
            Value::BulkString(b"not-json".to_vec()),
        );

        let entry = stream_id("1-0", map);

        let err = decode_typed_job::<SampleJob>("chat.message.created", &entry).unwrap_err();
        assert!(is_malformed(&err));
    }

    #[test]
    fn extract_envelope_fields_rejects_invalid_utf8() {
        let mut map = HashMap::new();
        map.insert(
            "type".to_owned(),
            Value::BulkString(b"chat.message.created".to_vec()),
        );
        map.insert(
            "payload".to_owned(),
            Value::BulkString(vec![0xff, 0xfe]),
        );

        let entry = stream_id("1-0", map);

        assert!(extract_envelope_fields(&entry).is_err());
    }
}
