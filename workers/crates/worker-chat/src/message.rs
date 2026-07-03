use anyhow::Result;
use redis::streams::StreamId;
use worker_core::message::decode_typed_job;

use crate::config::STREAM_KEY;
use crate::job::ChatMessageCreatedJob;

pub fn decode_chat_message_created(entry: &StreamId) -> Result<ChatMessageCreatedJob> {
    decode_typed_job(STREAM_KEY, entry)
}

#[cfg(test)]
mod tests {
    use std::collections::HashMap;

    use super::*;
    use redis::streams::StreamId;
    use redis::Value;
    use worker_core::error::is_malformed;

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

        let job = decode_chat_message_created(&entry).unwrap();
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

        let err = decode_chat_message_created(&entry).unwrap_err();
        assert!(is_malformed(&err));
    }

    #[test]
    fn malformed_entry_detects_unexpected_job_type() {
        let mut map = HashMap::new();
        map.insert(
            "type".to_owned(),
            Value::BulkString(b"location.cluster".to_vec()),
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

        let err = decode_chat_message_created(&entry).unwrap_err();
        assert!(is_malformed(&err));
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

        let err = decode_chat_message_created(&entry).unwrap_err();
        assert!(is_malformed(&err));
    }
}
