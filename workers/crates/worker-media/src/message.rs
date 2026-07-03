use anyhow::Result;
use redis::streams::StreamId;
use worker_core::error::wrap_malformed;
use worker_core::message::decode_typed_job;

use crate::config::STREAM_KEY;
use crate::job::MediaUploadedJob;

pub fn decode_media_uploaded(entry: &StreamId) -> Result<MediaUploadedJob> {
    let job: MediaUploadedJob = decode_typed_job(STREAM_KEY, entry)?;
    job.validate().map_err(wrap_malformed)?;
    Ok(job)
}

#[cfg(test)]
mod tests {
    use std::collections::HashMap;

    use super::*;
    use redis::streams::StreamId;
    use redis::Value;
    use worker_core::error::is_malformed;

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

        let job = decode_media_uploaded(&entry).unwrap();
        assert_eq!(job.media_asset_id, 9);
        assert_eq!(job.intended_context, "Post");
        assert_eq!(job.kind, "Video");
        assert_eq!(job.target_max_bytes, 2_147_483_648);
    }

    #[test]
    fn malformed_entry_detects_non_positive_target_max_bytes() {
        for target_max_bytes in [0, -1] {
            let mut map = HashMap::new();
            map.insert(
                "type".to_owned(),
                Value::BulkString(b"media.uploaded".to_vec()),
            );
            map.insert(
                "payload".to_owned(),
                Value::BulkString(
                    format!(
                        r#"{{"mediaAssetId":9,"intendedContext":"Post","kind":"Video","mimeType":"video/mp4","originalObjectKey":"raw/1/a.mp4","originalSizeBytes":500,"targetMaxBytes":{target_max_bytes}}}"#
                    )
                    .into_bytes(),
                ),
            );

            let entry = StreamId {
                id: "2-0".to_owned(),
                map,
            };

            let err = decode_media_uploaded(&entry).unwrap_err();
            assert!(is_malformed(&err), "targetMaxBytes={target_max_bytes}");
        }
    }

    #[test]
    fn malformed_entry_detects_invalid_json_payload() {
        let mut map = HashMap::new();
        map.insert(
            "type".to_owned(),
            Value::BulkString(b"media.uploaded".to_vec()),
        );
        map.insert(
            "payload".to_owned(),
            Value::BulkString(b"not-json".to_vec()),
        );

        let entry = StreamId {
            id: "2-0".to_owned(),
            map,
        };

        let err = decode_media_uploaded(&entry).unwrap_err();
        assert!(is_malformed(&err));
    }
}
