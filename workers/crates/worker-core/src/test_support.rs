use std::collections::HashMap;

use redis::streams::StreamId;
use redis::Value;

#[doc(hidden)]
pub fn stream_id(id: impl Into<String>, map: HashMap<String, Value>) -> StreamId {
    StreamId {
        id: id.into(),
        map,
        delivered_count: None,
        milliseconds_elapsed_from_delivery: None,
    }
}
