use serde::{Deserialize, Serialize};

/// Matches `Api.Global.Queue.ChatMessageCreatedJob` (System.Text.Json web defaults).
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ChatMessageCreatedJob {
    pub message_id: i64,
    pub chat_room_id: i64,
    pub sender_user_id: i64,
    pub body: String,
    pub sent_at: String,
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn deserializes_camel_case_json_from_api() {
        let job: ChatMessageCreatedJob = serde_json::from_str(
            r#"{"messageId":1,"chatRoomId":2,"senderUserId":3,"body":"hi","sentAt":"2026-01-01T00:00:00+00:00"}"#,
        )
        .unwrap();

        assert_eq!(job.message_id, 1);
        assert_eq!(job.chat_room_id, 2);
        assert_eq!(job.sender_user_id, 3);
        assert_eq!(job.body, "hi");
        assert_eq!(job.sent_at, "2026-01-01T00:00:00+00:00");
    }

    #[test]
    fn serializes_camel_case_json_for_api() {
        let job = ChatMessageCreatedJob {
            message_id: 1,
            chat_room_id: 2,
            sender_user_id: 3,
            body: "hi".to_owned(),
            sent_at: "2026-01-01T00:00:00+00:00".to_owned(),
        };

        let json = serde_json::to_string(&job).unwrap();
        assert!(json.contains("\"messageId\":1"));
        assert!(json.contains("\"chatRoomId\":2"));
    }
}
