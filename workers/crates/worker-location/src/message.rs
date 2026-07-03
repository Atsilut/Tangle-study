use anyhow::Result;
use redis::streams::StreamId;
use worker_core::error::wrap_malformed;
use worker_core::message::decode_typed_job;

use crate::config::STREAM_KEY;
use crate::job::LocationClusterJob;

pub fn decode_location_cluster(entry: &StreamId) -> Result<LocationClusterJob> {
    let job: LocationClusterJob = decode_typed_job(STREAM_KEY, entry)?;
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
    fn decodes_location_cluster_payload() {
        let mut map = HashMap::new();
        map.insert(
            "type".to_owned(),
            Value::BulkString(b"location.cluster".to_vec()),
        );
        map.insert(
            "payload".to_owned(),
            Value::BulkString(
                br#"{"minLatitude":37.0,"maxLatitude":38.0,"minLongitude":126.0,"maxLongitude":127.0,"zoom":3}"#
                    .to_vec(),
            ),
        );

        let entry = StreamId {
            id: "3-0".to_owned(),
            map,
        };

        let job = decode_location_cluster(&entry).unwrap();
        assert_eq!(job.zoom, 3);
    }

    #[test]
    fn malformed_entry_detects_invalid_zoom() {
        let mut map = HashMap::new();
        map.insert(
            "type".to_owned(),
            Value::BulkString(b"location.cluster".to_vec()),
        );
        map.insert(
            "payload".to_owned(),
            Value::BulkString(
                br#"{"minLatitude":37.0,"maxLatitude":38.0,"minLongitude":126.0,"maxLongitude":127.0,"zoom":10}"#
                    .to_vec(),
            ),
        );

        let entry = StreamId {
            id: "3-0".to_owned(),
            map,
        };

        let err = decode_location_cluster(&entry).unwrap_err();
        assert!(is_malformed(&err));
    }

    #[test]
    fn malformed_entry_detects_inverted_latitude_bounds() {
        let mut map = HashMap::new();
        map.insert(
            "type".to_owned(),
            Value::BulkString(b"location.cluster".to_vec()),
        );
        map.insert(
            "payload".to_owned(),
            Value::BulkString(
                br#"{"minLatitude":38.0,"maxLatitude":37.0,"minLongitude":126.0,"maxLongitude":127.0,"zoom":3}"#
                    .to_vec(),
            ),
        );

        let entry = StreamId {
            id: "3-0".to_owned(),
            map,
        };

        let err = decode_location_cluster(&entry).unwrap_err();
        assert!(is_malformed(&err));
    }

    #[test]
    fn malformed_entry_detects_invalid_json_payload() {
        let mut map = HashMap::new();
        map.insert(
            "type".to_owned(),
            Value::BulkString(b"location.cluster".to_vec()),
        );
        map.insert(
            "payload".to_owned(),
            Value::BulkString(b"not-json".to_vec()),
        );

        let entry = StreamId {
            id: "3-0".to_owned(),
            map,
        };

        let err = decode_location_cluster(&entry).unwrap_err();
        assert!(is_malformed(&err));
    }
}
